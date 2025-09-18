using BhavenaKhadiBhavan.Models;
using BhavenaKhadiBhavan.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BhavenaKhadiBhavan.Controllers
{
    public class HomeController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IProductService _productService;

        public HomeController(IReportService reportService, IProductService productService)
        {
            _reportService = reportService;
            _productService = productService;
        }

        /// <summary>
        /// Dashboard showing key business metrics
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var dashboard = await _reportService.GetDashboardDataAsync();
                return View(dashboard);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading dashboard: " + ex.Message;
                return View(new DashboardViewModel());
            }
        }

        /// <summary>
        /// Quick stock status for dashboard widgets
        /// </summary>
        public async Task<IActionResult> StockStatus()
        {
            try
            {
                var lowStockProducts = await _productService.GetLowStockProductsAsync();
                return PartialView("_StockStatusPartial", lowStockProducts);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading stock status: " + ex.Message;
                return PartialView("_StockStatusPartial", new List<Product>());
            }
        }

        /// <summary>
        /// Privacy page (required for default template)
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Error handling
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
