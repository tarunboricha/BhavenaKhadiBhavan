using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    /// <summary>
    /// Service for handling payment processing and reconciliation
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;

        public PaymentService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Process payment for a sale with potential adjustments
        /// </summary>
        public async Task<PaymentResult> ProcessPaymentAsync(int saleId, decimal amountReceived,
            string? adjustmentReason = null, string? processedBy = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = await _context.Sales.FindAsync(saleId);
                if (sale == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Sale not found"
                    };
                }

                // Calculate payment adjustment
                var calculatedTotal = sale.TotalAmount;
                var paymentAdjustment = amountReceived - calculatedTotal;

                // Update sale with payment details
                sale.CalculatedTotal = calculatedTotal;
                sale.AmountReceived = amountReceived;
                sale.PaymentAdjustment = paymentAdjustment;
                sale.TotalAmount = amountReceived; // Final amount is what customer paid
                sale.ProcessedBy = processedBy ?? "System";

                // Determine adjustment type and reason
                if (Math.Abs(paymentAdjustment) > 0.01m)
                {
                    sale.AdjustmentReason = adjustmentReason ?? DetermineAdjustmentReason(paymentAdjustment);
                    sale.AdjustmentType = DetermineAdjustmentType(paymentAdjustment, calculatedTotal);

                    // Check if requires approval
                    if (sale.ShouldRequireApproval)
                    {
                        sale.RequiresApproval = true;
                        sale.Status = "Pending Approval";
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new PaymentResult
                {
                    Success = true,
                    Message = GeneratePaymentMessage(sale),
                    PaymentAdjustment = paymentAdjustment,
                    RequiresApproval = sale.RequiresApproval,
                    AdjustmentType = sale.AdjustmentType
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new PaymentResult
                {
                    Success = false,
                    Message = $"Payment processing failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Approve payment adjustment (for manager use)
        /// </summary>
        public async Task<bool> ApprovePaymentAdjustmentAsync(int saleId, string approvedBy)
        {
            try
            {
                var sale = await _context.Sales.FindAsync(saleId);
                if (sale == null || !sale.RequiresApproval) return false;

                sale.ApprovedBy = approvedBy;
                sale.ApprovedAt = DateTime.Now;
                sale.RequiresApproval = false;
                sale.Status = "Completed";

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get sales requiring approval
        /// </summary>
        public async Task<List<Sale>> GetSalesRequiringApprovalAsync()
        {
            return await _context.Sales
                .Where(s => s.RequiresApproval)
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .OrderBy(s => s.SaleDate)
                .ToListAsync();
        }

        /// <summary>
        /// Get payment reconciliation report
        /// </summary>
        public async Task<PaymentReconciliationReport> GetPaymentReconciliationReportAsync(
            DateTime fromDate, DateTime toDate)
        {
            var sales = await _context.Sales
                .Where(s => s.SaleDate.Date >= fromDate.Date && s.SaleDate.Date <= toDate.Date)
                .Include(s => s.Customer)
                .ToListAsync();

            var report = new PaymentReconciliationReport
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalSales = sales.Count,
                TotalCalculatedAmount = sales.Sum(s => s.CalculatedTotal),
                TotalReceivedAmount = sales.Sum(s => s.AmountReceived),
                TotalAdjustment = sales.Sum(s => s.PaymentAdjustment),
                SalesWithAdjustments = sales.Count(s => s.HasPaymentAdjustment),
                ShortPayments = sales.Count(s => s.IsShortPayment),
                OverPayments = sales.Count(s => s.IsOverPayment),
                TotalShortAmount = sales.Where(s => s.IsShortPayment).Sum(s => Math.Abs(s.PaymentAdjustment)),
                TotalOverAmount = sales.Where(s => s.IsOverPayment).Sum(s => s.PaymentAdjustment),
                PendingApprovals = sales.Count(s => s.RequiresApproval)
            };

            // Group by adjustment types
            report.AdjustmentsByType = sales
                .Where(s => s.HasPaymentAdjustment)
                .GroupBy(s => s.AdjustmentType ?? "Unknown")
                .Select(g => new AdjustmentTypeSummary
                {
                    Type = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(s => Math.Abs(s.PaymentAdjustment)),
                    AverageAmount = g.Average(s => Math.Abs(s.PaymentAdjustment))
                })
                .ToList();

            // Daily breakdown
            report.DailyBreakdown = sales
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new DailyPaymentSummary
                {
                    Date = g.Key,
                    SalesCount = g.Count(),
                    CalculatedAmount = g.Sum(s => s.CalculatedTotal),
                    ReceivedAmount = g.Sum(s => s.AmountReceived),
                    AdjustmentAmount = g.Sum(s => s.PaymentAdjustment),
                    SalesWithAdjustments = g.Count(s => s.HasPaymentAdjustment)
                })
                .OrderBy(d => d.Date)
                .ToList();

            return report;
        }

        /// <summary>
        /// Get common adjustment reasons with statistics
        /// </summary>
        public async Task<List<AdjustmentReasonSummary>> GetAdjustmentReasonsAsync(DateTime fromDate, DateTime toDate)
        {
            return await _context.Sales
                .Where(s => s.SaleDate.Date >= fromDate.Date &&
                           s.SaleDate.Date <= toDate.Date &&
                           s.HasPaymentAdjustment)
                .GroupBy(s => s.AdjustmentReason ?? "No reason provided")
                .Select(g => new AdjustmentReasonSummary
                {
                    Reason = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(s => Math.Abs(s.PaymentAdjustment)),
                    AverageAmount = g.Average(s => Math.Abs(s.PaymentAdjustment)),
                    Percentage = 0 // Will be calculated after query
                })
                .OrderByDescending(r => r.Count)
                .ToListAsync();
        }

        // **PRIVATE HELPER METHODS**

        private string DetermineAdjustmentReason(decimal adjustment)
        {
            var absAdjustment = Math.Abs(adjustment);

            if (absAdjustment <= 5)
                return adjustment < 0 ? "Customer didn't have small change" : "Customer rounded up payment";

            if (absAdjustment <= 20)
                return adjustment < 0 ? "Customer short on cash" : "Customer paid extra";

            return adjustment < 0 ? "Significant underpayment" : "Significant overpayment";
        }

        private string DetermineAdjustmentType(decimal adjustment, decimal calculatedTotal)
        {
            var absAdjustment = Math.Abs(adjustment);
            var percentage = calculatedTotal > 0 ? (absAdjustment / calculatedTotal) * 100 : 0;

            if (absAdjustment <= 5 && percentage <= 1)
                return "Customer_Convenience";

            if (absAdjustment <= 20 && percentage <= 2)
                return "Cash_Shortage";

            if (percentage > 5)
                return "System_Error";

            return "Manager_Discretion";
        }

        private string GeneratePaymentMessage(Sale sale)
        {
            if (!sale.HasPaymentAdjustment)
                return $"Payment processed successfully. Exact amount received: ₹{sale.AmountReceived:N2}";

            if (sale.IsShortPayment)
            {
                var message = $"Payment processed with shortage of ₹{Math.Abs(sale.PaymentAdjustment):N2}. ";
                message += sale.RequiresApproval ? "Requires manager approval." : "Auto-approved.";
                return message;
            }

            if (sale.IsOverPayment)
            {
                return $"Payment processed with overpayment of ₹{sale.PaymentAdjustment:N2}. Customer paid extra.";
            }

            return "Payment processed successfully.";
        }
    }

    /// <summary>
    /// Interface for payment service
    /// </summary>
    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(int saleId, decimal amountReceived,
            string? adjustmentReason = null, string? processedBy = null);
        Task<bool> ApprovePaymentAdjustmentAsync(int saleId, string approvedBy);
        Task<List<Sale>> GetSalesRequiringApprovalAsync();
        Task<PaymentReconciliationReport> GetPaymentReconciliationReportAsync(DateTime fromDate, DateTime toDate);
        Task<List<AdjustmentReasonSummary>> GetAdjustmentReasonsAsync(DateTime fromDate, DateTime toDate);
    }
}