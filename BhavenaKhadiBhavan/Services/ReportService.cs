using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            return new DashboardViewModel
            {
                TodaySales = await _context.Sales
                    .Where(s => s.SaleDate.Date == today)
                    .SumAsync(s => s.TotalAmount),

                TodayOrders = await _context.Sales
                    .CountAsync(s => s.SaleDate.Date == today),

                MonthSales = await _context.Sales
                    .Where(s => s.SaleDate >= startOfMonth)
                    .SumAsync(s => s.TotalAmount),

                TotalCustomers = await _context.Customers.CountAsync(),

                LowStockCount = await _context.Products
                    .CountAsync(p => p.IsActive && p.StockQuantity <= p.MinimumStock),

                LowStockProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock)
                    .OrderBy(p => p.StockQuantity)
                    .Take(10)
                    .ToListAsync(),

                RecentSales = await _context.Sales
                    .Include(s => s.Customer)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(5)
                    .ToListAsync(),

                RecentCustomers = await _context.Customers
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(5)
                    .ToListAsync()
            };
        }

        public async Task<List<Sale>> GetSalesReportAsync(DateTime fromDate, DateTime toDate)
        {
            return await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Where(s => s.SaleDate.Date >= fromDate.Date && s.SaleDate.Date <= toDate.Date)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<List<Product>> GetStockReportAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<Dictionary<string, decimal>> GetSalesAnalyticsAsync(DateTime fromDate, DateTime toDate)
        {
            var sales = await _context.Sales
                .Where(s => s.SaleDate.Date >= fromDate.Date && s.SaleDate.Date <= toDate.Date)
                .ToListAsync();

            return new Dictionary<string, decimal>
            {
                {"TotalSales", sales.Sum(s => s.TotalAmount)},
                {"TotalOrders", sales.Count},
                {"AverageOrderValue", sales.Count > 0 ? sales.Average(s => s.TotalAmount) : 0},
                {"TotalGST", sales.Sum(s => s.GSTAmount)}
            };
        }

        public async Task<Dictionary<decimal, decimal>> GetGSTReportAsync(DateTime fromDate, DateTime toDate)
        {
            var saleItems = await _context.SaleItems
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate.Date >= fromDate.Date &&
                           si.Sale.SaleDate.Date <= toDate.Date &&
                           si.Sale.Status == "Completed")
                .ToListAsync();

            return saleItems
                .GroupBy(si => si.GSTRate)
                .ToDictionary(g => g.Key, g => g.Sum(si => si.GSTAmount));
        }
    }
}
