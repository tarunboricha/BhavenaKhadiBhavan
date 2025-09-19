using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    /// <summary>
    /// ENHANCED: Report Service Interface with Item-Level Discount Support and Profit Margin Analytics
    /// </summary>
    public interface IReportService
    {
        // Dashboard
        Task<DashboardViewModel> GetDashboardDataAsync();

        // ENHANCED: Sales Reports with Profit Margins
        Task<List<Sale>> GetSalesReportAsync(DateTime fromDate, DateTime toDate);
        Task<SalesReportViewModel> GetDetailedSalesReportAsync(DateTime fromDate, DateTime toDate);
        Task<List<Sale>> GetDailySalesReportAsync(DateTime date);
        Task<DailySalesReportViewModel> GetDetailedDailySalesReportAsync(DateTime date);

        // ENHANCED: Stock Reports with Comprehensive Analytics
        Task<List<Product>> GetStockReportAsync();
        Task<StockReportViewModel> GetDetailedStockReportAsync();
        Task<List<Product>> GetLowStockReportAsync();
        Task<StockMovementReportViewModel> GetStockMovementReportAsync(DateTime fromDate, DateTime toDate);

        // ENHANCED: Profit Analysis Reports
        Task<ProfitMarginReportViewModel> GetProfitMarginReportAsync(DateTime fromDate, DateTime toDate);
        Task<ProductProfitabilityReportViewModel> GetProductProfitabilityReportAsync(DateTime fromDate, DateTime toDate);

        // GST and Tax Reports
        Task<Dictionary<decimal, decimal>> GetGSTReportAsync(DateTime fromDate, DateTime toDate);
        Task<GSTReportViewModel> GetDetailedGSTReportAsync(DateTime fromDate, DateTime toDate);

        // Customer Analysis
        Task<CustomerReportViewModel> GetCustomerReportAsync(DateTime fromDate, DateTime toDate);

        // ENHANCED: Discount Analysis Reports
        Task<DiscountAnalysisReportViewModel> GetDiscountAnalysisReportAsync(DateTime fromDate, DateTime toDate);

        // Category Performance Reports
        Task<CategoryPerformanceReportViewModel> GetCategoryPerformanceReportAsync(DateTime fromDate, DateTime toDate);

        // Analytics
        Task<Dictionary<string, decimal>> GetSalesAnalyticsAsync(DateTime fromDate, DateTime toDate);

        // Return Analysis
        Task<ReturnAnalysisReportViewModel> GetReturnAnalysisReportAsync(DateTime fromDate, DateTime toDate);
    }
}