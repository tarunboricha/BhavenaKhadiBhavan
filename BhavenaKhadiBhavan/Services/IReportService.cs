using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface IReportService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
        Task<List<Product>> GetStockReportAsync();
        Task<Dictionary<decimal, decimal>> GetGSTReportAsync(DateTime fromDate, DateTime toDate);
        Task<List<Sale>> GetSalesReportAsync(DateTime fromDate, DateTime toDate);
        Task<Dictionary<string, decimal>> GetSalesAnalyticsAsync(DateTime fromDate, DateTime toDate);
    }
}
