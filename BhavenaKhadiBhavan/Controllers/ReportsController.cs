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
        /// Reports dashboard
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Sales report
        /// </summary>
        public async Task<IActionResult> Sales(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // Default to current month if no dates provided
                fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                toDate ??= DateTime.Today;

                var report = await _reportService.GetSalesReportAsync(fromDate.Value, toDate.Value);

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating sales report: " + ex.Message;
                return View(new ReportsViewModel());
            }
        }

        /// <summary>
        /// Stock/Inventory report
        /// </summary>
        public async Task<IActionResult> Stock()
        {
            try
            {
                var products = await _reportService.GetStockReportAsync();

                var stockSummary = new
                {
                    TotalProducts = products.Count,
                    ActiveProducts = products.Count(p => p.IsActive),
                    LowStockProducts = products.Count(p => p.IsLowStock),
                    OutOfStockProducts = products.Count(p => p.StockQuantity == 0),
                    TotalStockValue = products.Sum(p => p.StockQuantity * p.PurchasePrice),
                    TotalSaleValue = products.Sum(p => p.StockQuantity * p.SalePrice),
                    CategoryWiseStock = products
                        .Where(p => p.Category != null)
                        .GroupBy(p => p.Category!.Name)
                        .Select(g => new
                        {
                            Category = g.Key,
                            ProductCount = g.Count(),
                            TotalStock = g.Sum(p => p.StockQuantity),
                            StockValue = g.Sum(p => p.StockQuantity * p.PurchasePrice)
                        })
                        .OrderByDescending(x => x.StockValue)
                        .ToList()
                };

                ViewBag.StockSummary = stockSummary;
                return View(products);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating stock report: " + ex.Message;
                return View(new List<Product>());
            }
        }

        /// <summary>
        /// GST report for tax compliance
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
                    TotalGST = sales.Sum(s => s.GSTAmount),
                    GSTBreakdown = gstBreakdown,
                    Sales = sales,
                    GSTPercentage = sales.Sum(s => s.TotalAmount) > 0
                        ? (sales.Sum(s => s.GSTAmount) / sales.Sum(s => s.TotalAmount)) * 100
                        : 0
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
        /// Customer report
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
        /// Daily sales summary
        /// </summary>
        public async Task<IActionResult> DailySales(DateTime? date)
        {
            try
            {
                date ??= DateTime.Today;

                var sales = await _salesService.GetSalesByDateAsync(date.Value);

                var dailyReport = new
                {
                    Date = date.Value,
                    TotalSales = sales.Sum(s => s.TotalAmount),
                    TotalOrders = sales.Count,
                    TotalItems = sales.Sum(s => s.ItemCount),
                    TotalGST = sales.Sum(s => s.GSTAmount),
                    AverageOrderValue = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                    PaymentMethodBreakdown = sales
                        .GroupBy(s => s.PaymentMethod)
                        .Select(g => new
                        {
                            Method = g.Key,
                            Count = g.Count(),
                            Amount = g.Sum(s => s.TotalAmount)
                        })
                        .OrderByDescending(x => x.Amount)
                        .ToList(),
                    HourlySales = sales
                        .GroupBy(s => s.SaleDate.Hour)
                        .Select(g => new
                        {
                            Hour = g.Key,
                            Count = g.Count(),
                            Amount = g.Sum(s => s.TotalAmount)
                        })
                        .OrderBy(x => x.Hour)
                        .ToList(),
                    Sales = sales.OrderByDescending(s => s.SaleDate).ToList()
                };

                ViewBag.SelectedDate = date.Value.ToString("yyyy-MM-dd");
                return View(dailyReport);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating daily sales report: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// Low stock alert report
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
        /// Export sales report to CSV
        /// </summary>
        public async Task<IActionResult> ExportSalesToCSV(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var sales = await _salesService.GetSalesAsync(fromDate, toDate);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Invoice Number,Date,Customer,Phone,Payment Method,Items,Subtotal,GST,Discount,Total,Status");

                foreach (var sale in sales)
                {
                    csv.AppendLine($"\"{sale.InvoiceNumber}\",\"{sale.SaleDate:dd/MM/yyyy HH:mm}\"," +
                                  $"\"{sale.CustomerDisplayName}\",\"{sale.CustomerPhone}\"," +
                                  $"\"{sale.PaymentMethod}\",{sale.ItemCount}," +
                                  $"{sale.SubTotal:F2},{sale.GSTAmount:F2},{sale.DiscountAmount:F2}," +
                                  $"{sale.TotalAmount:F2},\"{sale.Status}\"");
                }

                var fileName = $"Sales_Report_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting sales report: " + ex.Message;
                return RedirectToAction(nameof(Sales));
            }
        }

        /// <summary>
        /// Export stock report to CSV
        /// </summary>
        public async Task<IActionResult> ExportStockToCSV()
        {
            try
            {
                var products = await _reportService.GetStockReportAsync();

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Product Name,Category,SKU,Fabric Type,Color,Size,Purchase Price,Sale Price," +
                              "Stock Quantity,Minimum Stock,Stock Value,Sale Value,Stock Status,Profit Margin");

                foreach (var product in products)
                {
                    csv.AppendLine($"\"{product.Name}\",\"{product.Category?.Name}\"," +
                                  $"\"{product.SKU}\",\"{product.FabricType}\",\"{product.Color}\"," +
                                  $"\"{product.Size}\",{product.PurchasePrice:F2},{product.SalePrice:F2}," +
                                  $"{product.StockQuantity},{product.MinimumStock}," +
                                  $"{(product.StockQuantity * product.PurchasePrice):F2}," +
                                  $"{(product.StockQuantity * product.SalePrice):F2}," +
                                  $"\"{product.StockStatus}\",{product.ProfitMargin:F2}%");
                }

                var fileName = $"Stock_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting stock report: " + ex.Message;
                return RedirectToAction(nameof(Stock));
            }
        }

        /// <summary>
        /// Print daily sales summary
        /// </summary>
        public async Task<IActionResult> PrintDailySales(DateTime date)
        {
            try
            {
                var sales = await _salesService.GetSalesByDateAsync(date);

                var report = new
                {
                    Date = date,
                    Sales = sales,
                    Summary = new
                    {
                        TotalSales = sales.Sum(s => s.TotalAmount),
                        TotalOrders = sales.Count,
                        TotalItems = sales.Sum(s => s.ItemCount),
                        TotalGST = sales.Sum(s => s.GSTAmount),
                        CashSales = sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount),
                        CardSales = sales.Where(s => s.PaymentMethod == "Card").Sum(s => s.TotalAmount),
                        UPISales = sales.Where(s => s.PaymentMethod == "UPI").Sum(s => s.TotalAmount)
                    }
                };

                return View(report);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating daily sales print: " + ex.Message;
                return RedirectToAction(nameof(DailySales), new { date });
            }
        }
    }
}
