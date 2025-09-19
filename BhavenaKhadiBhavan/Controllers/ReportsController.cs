using BhavenaKhadiBhavan.Models;
using BhavenaKhadiBhavan.Services;
using Microsoft.AspNetCore.Mvc;

namespace BhavenaKhadiBhavan.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ISalesService _salesService;
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;

        public ReportsController(
            IReportService reportService,
            ISalesService salesService,
            IProductService productService,
            ICustomerService customerService)
        {
            _reportService = reportService;
            _salesService = salesService;
            _productService = productService;
            _customerService = customerService;
        }

        /// <summary>
        /// Reports dashboard with enhanced analytics
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// ENHANCED: Sales report with profit margins and item-level discount analysis
        /// </summary>
        public async Task<IActionResult> Sales(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // Default to current month if no dates provided
                fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                toDate ??= DateTime.Today;

                var report = await _reportService.GetDetailedSalesReportAsync(fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating sales report: " + ex.Message;
                return View(new SalesReportViewModel());
            }
        }

        /// <summary>
        /// ENHANCED: Stock/Inventory report with comprehensive analytics
        /// </summary>
        public async Task<IActionResult> Stock()
        {
            try
            {
                var report = await _reportService.GetDetailedStockReportAsync();
                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating stock report: " + ex.Message;
                return View(new StockReportViewModel());
            }
        }

        /// <summary>
        /// ENHANCED: Daily sales report with hour-by-hour analysis
        /// </summary>
        public async Task<IActionResult> DailySales(DateTime? date)
        {
            try
            {
                date ??= DateTime.Today;

                var report = await _reportService.GetDetailedDailySalesReportAsync(date.Value);

                ViewBag.SelectedDate = date.Value.ToString("yyyy-MM-dd");
                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating daily sales report: " + ex.Message;
                return View(new DailySalesReportViewModel());
            }
        }

        /// <summary>
        /// NEW: Profit margin analysis report
        /// </summary>
        public async Task<IActionResult> ProfitMargin(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                toDate ??= DateTime.Today;

                var report = await _reportService.GetProfitMarginReportAsync(fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating profit margin report: " + ex.Message;
                return View(new ProfitMarginReportViewModel());
            }
        }

        /// <summary>
        /// NEW: Product profitability analysis
        /// </summary>
        public async Task<IActionResult> ProductProfitability(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                toDate ??= DateTime.Today;

                var report = await _reportService.GetProductProfitabilityReportAsync(fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating product profitability report: " + ex.Message;
                return View(new ProductProfitabilityReportViewModel());
            }
        }

        /// <summary>
        /// NEW: Item-level discount analysis report
        /// </summary>
        public async Task<IActionResult> DiscountAnalysis(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                toDate ??= DateTime.Today;

                var report = await _reportService.GetDiscountAnalysisReportAsync(fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating discount analysis report: " + ex.Message;
                return View(new DiscountAnalysisReportViewModel());
            }
        }

        /// <summary>
        /// ENHANCED: GST report for tax compliance with discount impact
        /// </summary>
        public async Task<IActionResult> GST(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // Default to current month if no dates provided
                fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                toDate ??= DateTime.Today;

                var gstBreakdown = await _reportService.GetGSTReportAsync(fromDate.Value, toDate.Value);
                var sales = await _salesService.GetSalesAsync(fromDate, toDate);

                var gstReport = new
                {
                    FromDate = fromDate.Value,
                    ToDate = toDate.Value,
                    TotalSales = sales.Sum(s => s.TotalAmount),
                    TotalTaxableAmount = sales.Sum(s => s.SubTotal - s.TotalItemDiscounts),
                    TotalGST = sales.Sum(s => s.GSTAmount),
                    TotalDiscounts = sales.Sum(s => s.TotalItemDiscounts),
                    GSTBreakdown = gstBreakdown,
                    Sales = sales,
                    GSTPercentage = sales.Sum(s => s.TotalAmount) > 0
                        ? (sales.Sum(s => s.GSTAmount) / sales.Sum(s => s.TotalAmount)) * 100
                        : 0,
                    SalesWithDiscounts = sales.Count(s => s.HasItemLevelDiscounts),
                    DiscountImpactOnGST = sales.Sum(s => s.TotalItemDiscounts * 0.05m) // Assuming 5% average GST
                };

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(gstReport);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating GST report: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// ENHANCED: Customer report with detailed analytics
        /// </summary>
        public async Task<IActionResult> Customers()
        {
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();

                var customerReport = new
                {
                    TotalCustomers = customers.Count,
                    NewCustomers = customers.Count(c => c.TotalOrders == 0),
                    ActiveCustomers = customers.Count(c => c.TotalOrders > 0),
                    RegularCustomers = customers.Count(c => c.TotalOrders >= 2 && c.TotalOrders < 5),
                    LoyalCustomers = customers.Count(c => c.TotalOrders >= 5),
                    InactiveCustomers = customers.Count(c => c.DaysSinceLastPurchase > 90),
                    TotalRevenue = customers.Sum(c => c.TotalPurchases),
                    AverageOrderValue = customers.Where(c => c.TotalOrders > 0).Any()
                        ? customers.Where(c => c.TotalOrders > 0).Average(c => c.AverageOrderValue)
                        : 0,
                    TopCustomers = customers
                        .Where(c => c.TotalOrders > 0)
                        .OrderByDescending(c => c.TotalPurchases)
                        .Take(20)
                        .ToList(),
                    RecentCustomers = customers
                        .OrderByDescending(c => c.CreatedAt)
                        .Take(20)
                        .ToList(),
                    CustomersByMonth = customers
                        .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                        .Select(g => new
                        {
                            Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                            Count = g.Count()
                        })
                        .OrderBy(x => x.Month)
                        .ToList()
                };

                return View(customerReport);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating customer report: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// ENHANCED: Low stock alert report with comprehensive analysis
        /// </summary>
        public async Task<IActionResult> LowStock()
        {
            try
            {
                var lowStockProducts = await _productService.GetLowStockProductsAsync();

                var lowStockReport = new
                {
                    TotalLowStockProducts = lowStockProducts.Count,
                    OutOfStockProducts = lowStockProducts.Count(p => p.StockQuantity == 0),
                    CriticalStockProducts = lowStockProducts.Count(p => p.StockQuantity <= 2),
                    TotalValueAtRisk = lowStockProducts.Sum(p => p.StockQuantity * p.SalePrice),
                    PotentialLostRevenue = lowStockProducts.Where(p => p.StockQuantity == 0).Sum(p => p.SalePrice * 10), // Estimate
                    CategoryWiseLowStock = lowStockProducts
                        .Where(p => p.Category != null)
                        .GroupBy(p => p.Category!.Name)
                        .Select(g => new
                        {
                            Category = g.Key,
                            Count = g.Count(),
                            Products = g.ToList()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList(),
                    Products = lowStockProducts.OrderBy(p => p.StockQuantity).ToList()
                };

                return View(lowStockReport);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating low stock report: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// ENHANCED: Export sales report to CSV with item-level discount details
        /// </summary>
        public async Task<IActionResult> ExportSalesToCSV(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var sales = await _salesService.GetSalesAsync(fromDate, toDate);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Invoice Number,Date,Customer,Phone,Payment Method,Items,Subtotal,Item Discounts,GST,Total,Profit Margin,Status");

                foreach (var sale in sales)
                {
                    var profitMargin = CalculateProfitMarginForSale(sale);

                    csv.AppendLine($"\"{sale.InvoiceNumber}\",\"{sale.SaleDate:dd/MM/yyyy HH:mm}\"," +
                                   $"\"{sale.CustomerDisplayName}\",\"{sale.CustomerPhone}\"," +
                                   $"\"{sale.PaymentMethod}\",{sale.ItemCount}," +
                                   $"{sale.SubTotal:F2},{sale.TotalItemDiscounts:F2},{sale.GSTAmount:F2}," +
                                   $"{sale.TotalAmount:F2},{profitMargin:F2}%,\"{sale.Status}\"");
                }

                var fileName = $"Enhanced_Sales_Report_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting sales report: " + ex.Message;
                return RedirectToAction(nameof(Sales));
            }
        }

        /// <summary>
        /// ENHANCED: Export stock report to CSV with profitability analysis
        /// </summary>
        public async Task<IActionResult> ExportStockToCSV()
        {
            try
            {
                var products = await _reportService.GetStockReportAsync();

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Product Name,Category,SKU,Fabric Type,Color,Size,Purchase Price,Sale Price," +
                              "Stock Quantity,Minimum Stock,Stock Value,Sale Value,Potential Profit,Stock Status,Profit Margin %,Unit of Measure");

                foreach (var product in products)
                {
                    var stockValue = product.StockQuantity * product.PurchasePrice;
                    var saleValue = product.StockQuantity * product.SalePrice;
                    var potentialProfit = saleValue - stockValue;

                    csv.AppendLine($"\"{product.Name}\",\"{product.Category?.Name}\"," +
                                   $"\"{product.SKU}\",\"{product.FabricType}\",\"{product.Color}\"," +
                                   $"\"{product.Size}\",{product.PurchasePrice:F2},{product.SalePrice:F2}," +
                                   $"{product.StockQuantity},{product.MinimumStock}," +
                                   $"{stockValue:F2},{saleValue:F2},{potentialProfit:F2}," +
                                   $"\"{product.StockStatus}\",{product.ProfitMargin:F2}%,\"{product.UnitOfMeasure}\"");
                }

                var fileName = $"Enhanced_Stock_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting stock report: " + ex.Message;
                return RedirectToAction(nameof(Stock));
            }
        }

        /// <summary>
        /// NEW: Export profit margin report to CSV
        /// </summary>
        public async Task<IActionResult> ExportProfitMarginToCSV(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var report = await _reportService.GetProfitMarginReportAsync(fromDate, toDate);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Product Name,Category,Quantity Sold,Revenue,Cost of Goods Sold,Gross Profit,Gross Profit Margin %,Total Discounts,Profit After Discounts");

                foreach (var product in report.ProductProfits)
                {
                    csv.AppendLine($"\"{product.ProductName}\",\"{product.CategoryName}\"," +
                                   $"{product.QuantitySold},{product.Revenue:F2},{product.CostOfGoodsSold:F2}," +
                                   $"{product.GrossProfit:F2},{product.GrossProfitMargin:F2}%," +
                                   $"{product.TotalDiscounts:F2},{product.ProfitAfterDiscounts:F2}");
                }

                var fileName = $"Profit_Margin_Report_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting profit margin report: " + ex.Message;
                return RedirectToAction(nameof(ProfitMargin));
            }
        }

        /// <summary>
        /// ENHANCED: Print daily sales summary with detailed analytics
        /// </summary>
        public async Task<IActionResult> PrintDailySales(DateTime date)
        {
            try
            {
                var report = await _reportService.GetDetailedDailySalesReportAsync(date);

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating daily sales print: " + ex.Message;
                return RedirectToAction(nameof(DailySales), new { date });
            }
        }

        /// <summary>
        /// NEW: Analytics dashboard with comprehensive KPIs
        /// </summary>
        public async Task<IActionResult> Analytics(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today;

                var salesAnalytics = await _reportService.GetSalesAnalyticsAsync(fromDate.Value, toDate.Value);
                var salesReport = await _reportService.GetDetailedSalesReportAsync(fromDate.Value, toDate.Value);

                var analytics = new
                {
                    FromDate = fromDate.Value,
                    ToDate = toDate.Value,
                    SalesAnalytics = salesAnalytics,
                    Profitability = new
                    {
                        TotalRevenue = salesReport.TotalSales,
                        TotalProfit = salesReport.TotalGrossProfit,
                        ProfitMargin = salesReport.GrossProfitMargin,
                        DiscountImpact = salesReport.TotalItemDiscounts
                    },
                    DiscountMetrics = new
                    {
                        DiscountPenetration = salesReport.DiscountPenetration,
                        AverageDiscount = salesReport.AverageDiscountPerSale,
                        SalesWithDiscounts = salesReport.SalesWithDiscounts
                    },
                    TopPerformers = salesReport.TopProducts.Take(5),
                    TrendData = salesReport.DailyBreakdown
                };

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(analytics);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading analytics: " + ex.Message;
                return View();
            }
        }

        #region Helper Methods

        private decimal CalculateProfitMarginForSale(Sale sale)
        {
            if (sale.SaleItems == null || !sale.SaleItems.Any())
                return 0;

            var totalCost = sale.SaleItems.Sum(si => si.Product?.PurchasePrice * si.Quantity ?? 0);
            var revenue = sale.TotalAmount;

            return revenue > 0 ? ((revenue - totalCost) / revenue) * 100 : 0;
        }

        #endregion
    }
}
