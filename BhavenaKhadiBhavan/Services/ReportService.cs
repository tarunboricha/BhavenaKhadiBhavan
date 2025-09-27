using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    /// <summary>
    /// ENHANCED: Report Service Implementation with Item-Level Discount Support and Comprehensive Analytics
    /// </summary>
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

        /// <summary>
        /// ENHANCED: Detailed Sales Report with Item-Level Discount Analysis
        /// </summary>
        public async Task<SalesReportViewModel> GetDetailedSalesReportAsync(DateTime fromDate, DateTime toDate)
        {
            var sales = await GetSalesReportAsync(fromDate, toDate);
            var saleItems = await _context.SaleItems
                .Include(si => si.Product)
                .ThenInclude(p => p.Category)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate.Date >= fromDate.Date && si.Sale.SaleDate.Date <= toDate.Date)
                .ToListAsync();

            var totalSubtotal = sales.Sum(s => s.SubTotal);
            var totalItemDiscounts = sales.Sum(s => s.TotalItemDiscounts);
            var totalGST = sales.Sum(s => s.GSTAmount);
            var totalCostOfGoodsSold = saleItems.Sum(si => si.Product != null ? si.Product.PurchasePrice * si.Quantity : 0);
            var totalSaleAmount = sales.Sum(s => s.TotalAmount);

            var report = new SalesReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                Sales = sales,

                // Basic Sales Summary
                TotalSales = sales.Sum(s => s.TotalAmount),
                TotalOrders = sales.Count,
                AverageOrderValue = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                TotalItemsSold = sales.Sum(s => s.ItemCount),

                // Financial Breakdown with Item-Level Discounts
                TotalSubtotal = totalSubtotal,
                TotalItemDiscounts = totalItemDiscounts,
                TotalGST = totalGST,
                TotalCostOfGoodsSold = totalCostOfGoodsSold,
                TotalGrossProfit = (totalSaleAmount) - totalCostOfGoodsSold,
                GrossProfitMargin = totalSubtotal > 0 ? (((totalSaleAmount) - totalCostOfGoodsSold) / (totalSaleAmount)) * 100 : 0,

                // Discount Analysis
                EffectiveDiscountPercentage = totalSubtotal > 0 ? (totalItemDiscounts / totalSubtotal) * 100 : 0,
                SalesWithDiscounts = sales.Count(s => s.HasItemLevelDiscounts),
                AverageDiscountPerSale = sales.Any() ? totalItemDiscounts / sales.Count : 0,
                DiscountPenetration = sales.Any() ? ((decimal)sales.Count(s => s.HasItemLevelDiscounts) / sales.Count) * 100 : 0,

                // Payment Method Breakdown
                PaymentMethodBreakdown = sales
                    .GroupBy(s => s.PaymentMethod)
                    .Select(g => new PaymentMethodSummary
                    {
                        Method = g.Key,
                        Count = g.Count(),
                        Amount = g.Sum(s => s.TotalAmount),
                        Percentage = sales.Any() ? ((decimal)g.Count() / sales.Count) * 100 : 0
                    })
                    .OrderByDescending(p => p.Amount)
                    .ToList(),

                // Daily Breakdown
                DailyBreakdown = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new DailySalesSummary
                    {
                        Date = g.Key,
                        Count = g.Count(),
                        Amount = g.Sum(s => s.TotalAmount),
                        GrossProfit = g.Sum(s => CalculateGrossProfitForSale(s)),
                        DiscountAmount = g.Sum(s => s.TotalItemDiscounts)
                    })
                    .OrderBy(d => d.Date)
                    .ToList(),

                // Top Products
                TopProducts = saleItems
                    .GroupBy(si => new { si.ProductId, si.ProductName, si.Product.Category.Name })
                    .Select(g => new TopProductSummary
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        CategoryName = g.Key.Name ?? "Unknown",
                        QuantitySold = g.Sum(si => si.Quantity),
                        Revenue = g.Sum(si => si.LineTotalWithDiscount),
                        GrossProfit = g.Sum(si => CalculateGrossProfitForItem(si)),
                        GrossProfitMargin = g.Sum(si => si.LineTotalWithDiscount) > 0 ?
                            (g.Sum(si => CalculateGrossProfitForItem(si)) / g.Sum(si => si.LineTotalWithDiscount)) * 100 : 0,
                        AverageDiscount = g.Average(si => si.ItemDiscountPercentage)
                    })
                    .OrderByDescending(p => p.Revenue)
                    .Take(20)
                    .ToList()
            };

            return report;
        }

        /// <summary>
        /// ENHANCED: Daily Sales Report with Hour-by-Hour Analysis
        /// </summary>
        public async Task<DailySalesReportViewModel> GetDetailedDailySalesReportAsync(DateTime date)
        {
            var sales = await GetDailySalesReportAsync(date);
            var saleItems = await _context.SaleItems
                .Include(si => si.Product)
                .ThenInclude(p => p.Category)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate.Date == date.Date)
                .ToListAsync();

            var report = new DailySalesReportViewModel
            {
                Date = date,
                Sales = sales,

                // Daily Summary
                TotalSales = sales.Sum(s => s.TotalAmount),
                TotalOrders = sales.Count,
                AverageOrderValue = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                TotalItemsSold = sales.Sum(s => s.ItemCount),
                TotalGrossProfit = sales.Sum(s => CalculateGrossProfitForSale(s)),
                GrossProfitMargin = sales.Sum(s => s.TotalAmount) > 0 ?
                    (sales.Sum(s => CalculateGrossProfitForSale(s)) / sales.Sum(s => s.TotalAmount)) * 100 : 0,

                // Hourly Breakdown
                HourlyBreakdown = sales
                    .GroupBy(s => s.SaleDate.Hour)
                    .Select(g => new HourlySalesSummary
                    {
                        Hour = g.Key,
                        Count = g.Count(),
                        Amount = g.Sum(s => s.TotalAmount),
                        GrossProfit = g.Sum(s => CalculateGrossProfitForSale(s))
                    })
                    .OrderBy(h => h.Hour)
                    .ToList(),

                // Payment Methods
                PaymentMethodBreakdown = sales
                    .GroupBy(s => s.PaymentMethod)
                    .Select(g => new PaymentMethodSummary
                    {
                        Method = g.Key,
                        Count = g.Count(),
                        Amount = g.Sum(s => s.TotalAmount),
                        Percentage = sales.Any() ? ((decimal)g.Count() / sales.Count) * 100 : 0
                    })
                    .ToList(),

                // Top Products of the Day
                TopProducts = saleItems
                    .GroupBy(si => new { si.ProductId, si.ProductName, si.Product.Category.Name })
                    .Select(g => new TopProductSummary
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        CategoryName = g.Key.Name ?? "Unknown",
                        QuantitySold = g.Sum(si => si.Quantity),
                        Revenue = g.Sum(si => si.LineTotalWithDiscount),
                        GrossProfit = g.Sum(si => CalculateGrossProfitForItem(si)),
                        AverageDiscount = g.Average(si => si.ItemDiscountPercentage)
                    })
                    .OrderByDescending(p => p.Revenue)
                    .Take(10)
                    .ToList(),

                // Customer Analysis
                UniqueCustomers = sales.Where(s => s.CustomerId.HasValue).Select(s => s.CustomerId).Distinct().Count(),
                NewCustomers = await _context.Customers.CountAsync(c => c.CreatedAt.Date == date.Date),
                ReturningCustomers = sales.Count(s => s.Customer != null && s.Customer.TotalOrders > 1)
            };

            return report;
        }

        /// <summary>
        /// ENHANCED: Comprehensive Stock Report
        /// </summary>
        public async Task<StockReportViewModel> GetDetailedStockReportAsync()
        {
            var products = await GetStockReportAsync();

            var report = new StockReportViewModel
            {
                Products = products,

                // Stock Summary
                TotalProducts = products.Count,
                ActiveProducts = products.Count(p => p.IsActive),
                LowStockProducts = products.Count(p => p.IsLowStock),
                OutOfStockProducts = products.Count(p => p.StockQuantity == 0),
                TotalStockValue = products.Sum(p => p.StockQuantity * p.PurchasePrice),
                TotalSaleValue = products.Sum(p => p.StockQuantity * p.SalePrice),
                PotentialProfit = products.Sum(p => p.StockQuantity * (p.SalePrice - p.PurchasePrice)),

                // Category Wise Stock
                CategoryWiseStock = products
                    .Where(p => p.Category != null)
                    .GroupBy(p => p.Category!.Name)
                    .Select(g => new CategoryStockSummary
                    {
                        Category = g.Key,
                        ProductCount = g.Count(),
                        TotalStock = g.Sum(p => p.StockQuantity),
                        StockValue = g.Sum(p => p.StockQuantity * p.PurchasePrice),
                        SaleValue = g.Sum(p => p.StockQuantity * p.SalePrice),
                        PotentialProfit = g.Sum(p => p.StockQuantity * (p.SalePrice - p.PurchasePrice))
                    })
                    .OrderByDescending(c => c.StockValue)
                    .ToList(),

                // Fast/Slow Moving Analysis (based on last 30 days sales)
                FastMovingProducts = await GetFastMovingProducts(30),
                SlowMovingProducts = await GetSlowMovingProducts(30),

                // Stock Age Analysis
                AverageStockAge = CalculateAverageStockAge(products),
                StockAging = GetStockAgingAnalysis(products)
            };

            return report;
        }

        /// <summary>
        /// ENHANCED: Profit Margin Report with Item-Level Discount Impact
        /// </summary>
        public async Task<ProfitMarginReportViewModel> GetProfitMarginReportAsync(DateTime fromDate, DateTime toDate)
        {
            var sales = await GetSalesReportAsync(fromDate, toDate);
            var saleItems = await _context.SaleItems
                .Include(si => si.Product)
                .ThenInclude(p => p.Category)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate.Date >= fromDate.Date && si.Sale.SaleDate.Date <= toDate.Date)
                .ToListAsync();

            var totalRevenue = sales.Sum(s => s.TotalAmount);
            var totalCOGS = saleItems.Sum(si => si.Product != null ? si.Product.PurchasePrice * si.Quantity : 0);
            var totalDiscounts = sales.Sum(s => s.TotalItemDiscounts);

            var report = new ProfitMarginReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,

                // Overall Profit Analysis
                TotalRevenue = totalRevenue,
                TotalCostOfGoodsSold = totalCOGS,
                TotalGrossProfit = totalRevenue - totalCOGS,
                GrossProfitMargin = totalRevenue > 0 ? ((totalRevenue - totalCOGS) / totalRevenue) * 100 : 0,

                // Impact of Discounts on Profit
                ProfitWithoutDiscounts = (sales.Sum(s => s.SubTotal) - totalCOGS),
                ProfitWithDiscounts = totalRevenue - totalCOGS,
                DiscountImpactOnProfit = totalDiscounts,

                // Category-wise Profit
                CategoryProfits = saleItems
                    .Where(si => si.Product?.Category != null)
                    .GroupBy(si => si.Product.Category.Name)
                    .Select(g => new CategoryProfitSummary
                    {
                        CategoryName = g.Key,
                        Revenue = g.Sum(si => si.LineTotalWithDiscount),
                        CostOfGoodsSold = g.Sum(si => si.Product.PurchasePrice * si.Quantity),
                        GrossProfit = g.Sum(si => si.LineTotalWithDiscount - (si.Product.PurchasePrice * si.Quantity)),
                        GrossProfitMargin = g.Sum(si => si.LineTotalWithDiscount) > 0 ?
                            ((g.Sum(si => si.LineTotalWithDiscount - (si.Product.PurchasePrice * si.Quantity)) / g.Sum(si => si.LineTotalWithDiscount)) * 100) : 0,
                        TotalDiscounts = g.Sum(si => si.ItemDiscountAmount),
                        ProductsSold = g.Count()
                    })
                    .OrderByDescending(c => c.GrossProfit)
                    .ToList(),

                // Product-wise Profit Analysis
                ProductProfits = saleItems
                    .Where(si => si.Product != null)
                    .GroupBy(si => new { si.ProductId, si.ProductName, si.Product.Category.Name })
                    .Select(g => new ProductProfitSummary
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        CategoryName = g.Key.Name ?? "Unknown",
                        QuantitySold = g.Sum(si => si.Quantity),
                        Revenue = g.Sum(si => si.LineTotalWithDiscount),
                        CostOfGoodsSold = g.Sum(si => si.Product.PurchasePrice * si.Quantity),
                        GrossProfit = g.Sum(si => si.LineTotalWithDiscount - (si.Product.PurchasePrice * si.Quantity)),
                        GrossProfitMargin = g.Sum(si => si.LineTotalWithDiscount) > 0 ?
                            ((g.Sum(si => si.LineTotalWithDiscount - (si.Product.PurchasePrice * si.Quantity)) / g.Sum(si => si.LineTotalWithDiscount)) * 100) : 0,
                        TotalDiscounts = g.Sum(si => si.ItemDiscountAmount),
                        ProfitAfterDiscounts = g.Sum(si => si.LineTotalWithDiscount - (si.Product.PurchasePrice * si.Quantity))
                    })
                    .OrderByDescending(p => p.GrossProfit)
                    .ToList(),

                // Daily Profit Trends
                DailyProfitTrends = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new DailyProfitSummary
                    {
                        Date = g.Key,
                        Revenue = g.Sum(s => s.TotalAmount),
                        CostOfGoodsSold = g.Sum(s => CalculateCOGSForSale(s)),
                        GrossProfit = g.Sum(s => s.TotalAmount - CalculateCOGSForSale(s)),
                        GrossProfitMargin = g.Sum(s => s.TotalAmount) > 0 ?
                            ((g.Sum(s => s.TotalAmount - CalculateCOGSForSale(s)) / g.Sum(s => s.TotalAmount)) * 100) : 0
                    })
                    .OrderBy(d => d.Date)
                    .ToList()
            };

            return report;
        }

        /// <summary>
        /// ENHANCED: Product Profitability Analysis
        /// </summary>
        public async Task<ProductProfitabilityReportViewModel> GetProductProfitabilityReportAsync(DateTime fromDate, DateTime toDate)
        {
            var profitReport = await GetProfitMarginReportAsync(fromDate, toDate);
            var productProfits = profitReport.ProductProfits;

            return new ProductProfitabilityReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,

                MostProfitableProducts = productProfits
                    .OrderByDescending(p => p.GrossProfit)
                    .Take(20)
                    .ToList(),

                LeastProfitableProducts = productProfits
                    .OrderBy(p => p.GrossProfit)
                    .Take(20)
                    .ToList(),

                HighMarginProducts = productProfits
                    .Where(p => p.GrossProfitMargin > 50)
                    .OrderByDescending(p => p.GrossProfitMargin)
                    .ToList(),

                MediumMarginProducts = productProfits
                    .Where(p => p.GrossProfitMargin >= 20 && p.GrossProfitMargin <= 50)
                    .OrderByDescending(p => p.GrossProfitMargin)
                    .ToList(),

                LowMarginProducts = productProfits
                    .Where(p => p.GrossProfitMargin >= 0 && p.GrossProfitMargin < 20)
                    .OrderByDescending(p => p.GrossProfitMargin)
                    .ToList(),

                LossProducts = productProfits
                    .Where(p => p.GrossProfitMargin < 0)
                    .OrderBy(p => p.GrossProfitMargin)
                    .ToList()
            };
        }

        /// <summary>
        /// ENHANCED: Discount Analysis Report
        /// </summary>
        public async Task<DiscountAnalysisReportViewModel> GetDiscountAnalysisReportAsync(DateTime fromDate, DateTime toDate)
        {
            var sales = await GetSalesReportAsync(fromDate, toDate);
            var saleItems = await _context.SaleItems
                .Include(si => si.Product)
                .ThenInclude(p => p.Category)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate.Date >= fromDate.Date && si.Sale.SaleDate.Date <= toDate.Date)
                .ToListAsync();

            var totalDiscounts = sales.Sum(s => s.TotalItemDiscounts);
            var salesWithDiscounts = sales.Count(s => s.HasItemLevelDiscounts);

            return new DiscountAnalysisReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,

                // Discount Summary
                TotalDiscountsGiven = totalDiscounts,
                AverageDiscountPercentage = salesWithDiscounts > 0 ?
                    sales.Where(s => s.HasItemLevelDiscounts).Average(s => s.EffectiveDiscountPercentage) : 0,
                SalesWithDiscounts = salesWithDiscounts,
                DiscountPenetration = sales.Any() ? ((decimal)salesWithDiscounts / sales.Count) * 100 : 0,

                // Item-Level Discount Analysis
                ProductDiscountAnalysis = saleItems
                    .Where(si => si.HasItemDiscount)
                    .GroupBy(si => new { si.ProductId, si.ProductName, si.Product.Category.Name })
                    .Select(g => new ProductDiscountSummary
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        CategoryName = g.Key.Name ?? "Unknown",
                        AverageDiscountPercentage = g.Average(si => si.ItemDiscountPercentage),
                        TotalDiscountAmount = g.Sum(si => si.ItemDiscountAmount),
                        TimesDiscounted = g.Count(),
                        QuantitySold = g.Sum(si => si.Quantity),
                        RevenueImpact = g.Sum(si => si.ItemDiscountAmount)
                    })
                    .OrderByDescending(p => p.TotalDiscountAmount)
                    .ToList(),

                // Discount Impact Analysis
                RevenueImpact = totalDiscounts,
                ProfitImpact = totalDiscounts, // Discounts directly reduce profit
                GSTImpact = saleItems.Sum(si => si.ItemDiscountAmount * si.GSTRate / 100),

                // Daily Discount Trends
                DailyDiscountTrends = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new DailyDiscountSummary
                    {
                        Date = g.Key,
                        TotalDiscounts = g.Sum(s => s.TotalItemDiscounts),
                        AverageDiscountPercentage = g.Where(s => s.HasItemLevelDiscounts).Any() ?
                            g.Where(s => s.HasItemLevelDiscounts).Average(s => s.EffectiveDiscountPercentage) : 0,
                        SalesWithDiscounts = g.Count(s => s.HasItemLevelDiscounts),
                        DiscountPenetration = g.Any() ? ((decimal)g.Count(s => s.HasItemLevelDiscounts) / g.Count()) * 100 : 0
                    })
                    .OrderBy(d => d.Date)
                    .ToList(),

                // Most Discounted Products
                MostDiscountedProducts = saleItems
                    .Where(si => si.HasItemDiscount)
                    .GroupBy(si => new { si.ProductId, si.ProductName, si.Product.Category.Name })
                    .Select(g => new ProductDiscountSummary
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        CategoryName = g.Key.Name ?? "Unknown",
                        AverageDiscountPercentage = g.Average(si => si.ItemDiscountPercentage),
                        TotalDiscountAmount = g.Sum(si => si.ItemDiscountAmount),
                        TimesDiscounted = g.Count(),
                        QuantitySold = g.Sum(si => si.Quantity),
                        RevenueImpact = g.Sum(si => si.ItemDiscountAmount)
                    })
                    .OrderByDescending(p => p.AverageDiscountPercentage)
                    .Take(20)
                    .ToList()
            };
        }

        // Standard interface implementations
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

        public async Task<List<Sale>> GetDailySalesReportAsync(DateTime date)
        {
            return await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Where(s => s.SaleDate.Date == date.Date)
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
                {"TotalGST", sales.Sum(s => s.GSTAmount)},
                {"TotalDiscounts", sales.Sum(s => s.TotalItemDiscounts)},
                {"SalesWithDiscounts", sales.Count(s => s.HasItemLevelDiscounts)}
            };
        }

        // TODO: Implement remaining enhanced report methods
        // These would follow similar patterns for Customer, Category Performance, Return Analysis, etc.

        #region Helper Methods

        private decimal CalculateGrossProfitForSale(Sale sale)
        {
            if (sale.SaleItems == null || !sale.SaleItems.Any())
                return 0;

            return sale.SaleItems.Sum(si => CalculateGrossProfitForItem(si));
        }

        private decimal CalculateGrossProfitForItem(SaleItem saleItem)
        {
            if (saleItem.Product == null)
                return 0;

            var revenue = saleItem.LineTotalWithDiscount;
            var cost = saleItem.Product.PurchasePrice * saleItem.Quantity;
            return revenue - cost;
        }

        private decimal CalculateCOGSForSale(Sale sale)
        {
            if (sale.SaleItems == null || !sale.SaleItems.Any())
                return 0;

            return sale.SaleItems.Sum(si => si.Product != null ? si.Product.PurchasePrice * si.Quantity : 0);
        }

        private async Task<List<Product>> GetFastMovingProducts(int days)
        {
            var fromDate = DateTime.Today.AddDays(-days);
            var fastMoving = await _context.SaleItems
                .Include(si => si.Product)
                .ThenInclude(p => p.Category)
                .Where(si => si.Sale.SaleDate >= fromDate)
                .GroupBy(si => si.Product)
                .Select(g => new { Product = g.Key, TotalSold = g.Sum(si => si.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(20)
                .Select(x => x.Product)
                .ToListAsync();

            return fastMoving;
        }

        private async Task<List<Product>> GetSlowMovingProducts(int days)
        {
            var fromDate = DateTime.Today.AddDays(-days);
            var soldProductIds = await _context.SaleItems
                .Where(si => si.Sale.SaleDate >= fromDate)
                .Select(si => si.ProductId)
                .Distinct()
                .ToListAsync();

            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && !soldProductIds.Contains(p.Id) && p.StockQuantity > 0)
                .OrderByDescending(p => p.StockQuantity)
                .Take(20)
                .ToListAsync();
        }

        private decimal CalculateAverageStockAge(List<Product> products)
        {
            return products.Any(p => p.StockQuantity > 0)
                ? products.Where(p => p.StockQuantity > 0)
                          .Average(p => (decimal)(DateTime.Now - p.UpdatedAt).Days)
                : 0;
        }

        private List<StockAgingSummary> GetStockAgingAnalysis(List<Product> products)
        {
            // Simplified aging analysis
            var stockWithAge = products
                .Where(p => p.StockQuantity > 0)
                .Select(p => new { Product = p, Age = (DateTime.Now - p.UpdatedAt).Days })
                .ToList();

            return new List<StockAgingSummary>
            {
                new() {
                    AgeRange = "0-30 days",
                    ProductCount = stockWithAge.Count(s => s.Age <= 30),
                    StockValue = stockWithAge.Where(s => s.Age <= 30).Sum(s => s.Product.StockQuantity * s.Product.PurchasePrice)
                },
                new() {
                    AgeRange = "31-60 days",
                    ProductCount = stockWithAge.Count(s => s.Age > 30 && s.Age <= 60),
                    StockValue = stockWithAge.Where(s => s.Age > 30 && s.Age <= 60).Sum(s => s.Product.StockQuantity * s.Product.PurchasePrice)
                },
                new() {
                    AgeRange = "61-90 days",
                    ProductCount = stockWithAge.Count(s => s.Age > 60 && s.Age <= 90),
                    StockValue = stockWithAge.Where(s => s.Age > 60 && s.Age <= 90).Sum(s => s.Product.StockQuantity * s.Product.PurchasePrice)
                },
                new() {
                    AgeRange = "90+ days",
                    ProductCount = stockWithAge.Count(s => s.Age > 90),
                    StockValue = stockWithAge.Where(s => s.Age > 90).Sum(s => s.Product.StockQuantity * s.Product.PurchasePrice)
                }
            };
        }

        #endregion

        // Placeholder implementations for remaining interface methods
        public Task<List<Product>> GetLowStockReportAsync() => throw new NotImplementedException();
        public Task<StockMovementReportViewModel> GetStockMovementReportAsync(DateTime fromDate, DateTime toDate) => throw new NotImplementedException();
        public Task<GSTReportViewModel> GetDetailedGSTReportAsync(DateTime fromDate, DateTime toDate) => throw new NotImplementedException();
        public Task<CustomerReportViewModel> GetCustomerReportAsync(DateTime fromDate, DateTime toDate) => throw new NotImplementedException();
        public Task<CategoryPerformanceReportViewModel> GetCategoryPerformanceReportAsync(DateTime fromDate, DateTime toDate) => throw new NotImplementedException();
        public Task<ReturnAnalysisReportViewModel> GetReturnAnalysisReportAsync(DateTime fromDate, DateTime toDate) => throw new NotImplementedException();
    }
}