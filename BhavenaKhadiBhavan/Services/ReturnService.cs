using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    public class ReturnService : IReturnService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReturnService> _logger;

        public ReturnService(ApplicationDbContext context, ILogger<ReturnService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get returns with filtering and search
        /// </summary>
        public async Task<List<Return>> GetReturnsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? search = null, string? status = null)
        {
            var query = _context.Returns
                .Include(r => r.Sale)
                .ThenInclude(s => s.Customer)
                .Include(r => r.ReturnItems)
                .ThenInclude(ri => ri.Product)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.ReturnDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.ReturnDate.Date <= toDate.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(r =>
                    r.ReturnNumber.ToLower().Contains(search) ||
                    r.Sale.InvoiceNumber.ToLower().Contains(search) ||
                    r.Sale.CustomerName != null && r.Sale.CustomerName.ToLower().Contains(search) ||
                    r.Sale.CustomerPhone != null && r.Sale.CustomerPhone.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            return await query
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();
        }

        /// <summary>
        /// Get return by ID with all related data
        /// </summary>
        public async Task<Return?> GetReturnByIdAsync(int id)
        {
            return await _context.Returns
                .Include(r => r.Sale)
                .ThenInclude(s => s.Customer)
                .Include(r => r.Sale)
                .ThenInclude(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Include(r => r.ReturnItems)
                .ThenInclude(ri => ri.Product)
                .Include(r => r.ReturnItems)
                .ThenInclude(ri => ri.SaleItem)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        /// <summary>
        /// Create new return with item-level discount handling
        /// </summary>
        public async Task<Return> CreateReturnAsync(Return returnTransaction, List<ReturnItemViewModel> returnItems)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Creating return for sale {SaleId} with {ItemCount} items",
                    returnTransaction.SaleId, returnItems.Count);

                // Generate return number
                returnTransaction.ReturnNumber = await GenerateReturnNumberAsync();

                // Calculate totals
                var totals = ReturnCalculator.CalculateReturnTotals(returnItems);
                returnTransaction.SubTotal = totals.subtotal;
                returnTransaction.TotalItemDiscounts = totals.totalDiscounts;
                returnTransaction.GSTAmount = totals.totalGST;
                returnTransaction.RefundAmount = totals.refundAmount;

                // Add return to context
                _context.Returns.Add(returnTransaction);
                await _context.SaveChangesAsync();

                // Create return items and update sale item returned quantities
                foreach (var returnItemVM in returnItems)
                {
                    // Get original sale item
                    var saleItem = await _context.SaleItems.FindAsync(returnItemVM.SaleItemId);
                    if (saleItem == null)
                    {
                        throw new InvalidOperationException($"Sale item {returnItemVM.SaleItemId} not found");
                    }

                    // Create return item
                    var returnItem = new ReturnItem
                    {
                        ReturnId = returnTransaction.Id,
                        SaleItemId = returnItemVM.SaleItemId,
                        ProductId = returnItemVM.ProductId,
                        ProductName = returnItemVM.ProductName,
                        ReturnQuantity = returnItemVM.ReturnQuantity,
                        UnitPrice = returnItemVM.UnitPrice,
                        GSTRate = returnItemVM.GSTRate,
                        UnitOfMeasure = returnItemVM.UnitOfMeasure,
                        OriginalItemDiscountPercentage = returnItemVM.OriginalItemDiscountPercentage,
                        ProportionalDiscountAmount = returnItemVM.ProportionalDiscountAmount,
                        Status = ReturnStatus.Pending,
                        Condition = returnItemVM.Condition
                    };

                    // Calculate line totals
                    var lineCalc = ReturnCalculator.CalculateReturnLineTotal(
                        returnItem.UnitPrice,
                        returnItem.ReturnQuantity,
                        returnItem.GSTRate,
                        returnItem.ProportionalDiscountAmount);

                    returnItem.GSTAmount = lineCalc.gst;
                    returnItem.LineTotal = lineCalc.total;

                    _context.ReturnItems.Add(returnItem);

                    // Update sale item returned quantity
                    saleItem.ReturnedQuantity += returnItemVM.ReturnQuantity;

                    _logger.LogInformation("Processing return item: {ProductName}, Qty: {Qty}, Discount: ₹{Discount}",
                        returnItem.ProductName, returnItem.ReturnQuantity, returnItem.ProportionalDiscountAmount);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Return created successfully: {ReturnNumber}, Amount: ₹{Amount}, Discounts: ₹{Discounts}",
                    returnTransaction.ReturnNumber, returnTransaction.RefundAmount, returnTransaction.TotalItemDiscounts);

                return returnTransaction;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating return for sale {SaleId}", returnTransaction.SaleId);
                throw;
            }
        }

        /// <summary>
        /// Process return (approve and issue refund) with stock restoration
        /// </summary>
        public async Task<ReturnProcessResult> ProcessReturnAsync(int returnId, string refundMethod, string? refundReference, string? notes, string? processedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var returnTransaction = await GetReturnByIdAsync(returnId);
                if (returnTransaction == null)
                {
                    return new ReturnProcessResult
                    {
                        Success = false,
                        ErrorMessage = "Return not found"
                    };
                }

                if (!returnTransaction.CanBeProcessed)
                {
                    return new ReturnProcessResult
                    {
                        Success = false,
                        ErrorMessage = $"Return cannot be processed. Current status: {returnTransaction.Status}"
                    };
                }

                _logger.LogInformation("Processing return {ReturnNumber} for refund amount ₹{Amount}",
                    returnTransaction.ReturnNumber, returnTransaction.RefundAmount);

                // Update return status and processing details
                returnTransaction.Status = ReturnStatus.Completed;
                returnTransaction.RefundMethod = refundMethod;
                returnTransaction.RefundReference = refundReference;
                returnTransaction.ProcessedDate = DateTime.Now;
                returnTransaction.ProcessedBy = processedBy;

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    returnTransaction.Notes = string.IsNullOrWhiteSpace(returnTransaction.Notes)
                        ? notes
                        : returnTransaction.Notes + "\n\nProcessing Notes: " + notes;
                }

                // Update return items status and restore stock
                foreach (var returnItem in returnTransaction.ReturnItems)
                {
                    returnItem.Status = ReturnStatus.Completed;

                    // Restore stock to inventory
                    var product = await _context.Products.FindAsync(returnItem.ProductId);
                    if (product != null)
                    {
                        _logger.LogInformation("Restoring stock for {ProductName}: +{Quantity} {Unit}",
                            product.Name, returnItem.ReturnQuantity, returnItem.UnitOfMeasure);

                        product.StockQuantity += returnItem.ReturnQuantity;
                        product.UpdatedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Return processed successfully: {ReturnNumber}, Refund: ₹{Amount} via {Method}",
                    returnTransaction.ReturnNumber, returnTransaction.RefundAmount, refundMethod);

                return new ReturnProcessResult
                {
                    Success = true,
                    RefundAmount = returnTransaction.RefundAmount,
                    ReturnNumber = returnTransaction.ReturnNumber
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing return {ReturnId}", returnId);
                return new ReturnProcessResult
                {
                    Success = false,
                    ErrorMessage = "Error processing return: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Cancel return and restore sale item quantities
        /// </summary>
        public async Task<ReturnProcessResult> CancelReturnAsync(int returnId, string reason, string? cancelledBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var returnTransaction = await GetReturnByIdAsync(returnId);
                if (returnTransaction == null)
                {
                    return new ReturnProcessResult
                    {
                        Success = false,
                        ErrorMessage = "Return not found"
                    };
                }

                if (returnTransaction.Status == ReturnStatus.Completed)
                {
                    return new ReturnProcessResult
                    {
                        Success = false,
                        ErrorMessage = "Cannot cancel a completed return"
                    };
                }

                _logger.LogInformation("Cancelling return {ReturnNumber}", returnTransaction.ReturnNumber);

                // Update return status
                returnTransaction.Status = ReturnStatus.Cancelled;
                returnTransaction.ProcessedDate = DateTime.Now;
                returnTransaction.ProcessedBy = cancelledBy;
                returnTransaction.Notes = string.IsNullOrWhiteSpace(returnTransaction.Notes)
                    ? $"Cancelled: {reason}"
                    : returnTransaction.Notes + $"\n\nCancelled: {reason}";

                // Update return items status
                foreach (var returnItem in returnTransaction.ReturnItems)
                {
                    returnItem.Status = ReturnStatus.Cancelled;

                    // Restore sale item returned quantity
                    var saleItem = await _context.SaleItems.FindAsync(returnItem.SaleItemId);
                    if (saleItem != null)
                    {
                        saleItem.ReturnedQuantity -= returnItem.ReturnQuantity;
                        if (saleItem.ReturnedQuantity < 0)
                        {
                            saleItem.ReturnedQuantity = 0; // Safety check
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Return cancelled successfully: {ReturnNumber}", returnTransaction.ReturnNumber);

                return new ReturnProcessResult
                {
                    Success = true,
                    ReturnNumber = returnTransaction.ReturnNumber
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling return {ReturnId}", returnId);
                return new ReturnProcessResult
                {
                    Success = false,
                    ErrorMessage = "Error cancelling return: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Get returnable items for a sale with discount information
        /// </summary>
        public async Task<List<ReturnableItemViewModel>> GetReturnableItemsAsync(int saleId)
        {
            var saleItems = await _context.SaleItems
                .Include(si => si.Product)
                .Where(si => si.SaleId == saleId)
                .ToListAsync();

            var returnableItems = new List<ReturnableItemViewModel>();

            foreach (var saleItem in saleItems)
            {
                var returnableQuantity = saleItem.Quantity - saleItem.ReturnedQuantity;

                if (returnableQuantity > 0)
                {
                    returnableItems.Add(new ReturnableItemViewModel
                    {
                        SaleItemId = saleItem.Id,
                        ProductId = saleItem.ProductId,
                        ProductName = saleItem.ProductName,
                        OriginalQuantity = saleItem.Quantity,
                        ReturnedQuantity = saleItem.ReturnedQuantity,
                        ReturnableQuantity = returnableQuantity,
                        UnitPrice = saleItem.UnitPrice,
                        GSTRate = saleItem.GSTRate,
                        UnitOfMeasure = saleItem.UnitOfMeasure ?? "Piece",
                        OriginalItemDiscountPercentage = saleItem.ItemDiscountPercentage,
                        OriginalItemDiscountAmount = saleItem.ItemDiscountAmount
                    });
                }
            }

            return returnableItems;
        }

        /// <summary>
        /// Get available quantities for return validation
        /// </summary>
        public async Task<Dictionary<int, decimal>> GetAvailableQuantitiesForReturnAsync(int saleId)
        {
            var saleItems = await _context.SaleItems
                .Where(si => si.SaleId == saleId)
                .Select(si => new { si.Id, si.Quantity, si.ReturnedQuantity })
                .ToListAsync();

            return saleItems.ToDictionary(
                si => si.Id,
                si => si.Quantity - si.ReturnedQuantity
            );
        }

        /// <summary>
        /// Generate unique return number
        /// </summary>
        private async Task<string> GenerateReturnNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"RET{today:yyyyMMdd}";

            var lastReturn = await _context.Returns
                .Where(r => r.ReturnNumber.StartsWith(prefix))
                .OrderByDescending(r => r.ReturnNumber)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastReturn != null && lastReturn.ReturnNumber.Length > prefix.Length)
            {
                var lastSequence = lastReturn.ReturnNumber.Substring(prefix.Length);
                if (int.TryParse(lastSequence, out int lastNum))
                {
                    sequence = lastNum + 1;
                }
            }

            return $"{prefix}{sequence:D3}";
        }
    }

    // =========================================
    // Service Registration Extension
    // =========================================

    public static class ReturnServiceExtensions
    {
        public static IServiceCollection AddReturnService(this IServiceCollection services)
        {
            services.AddScoped<IReturnService, ReturnService>();
            return services;
        }
    }
}
