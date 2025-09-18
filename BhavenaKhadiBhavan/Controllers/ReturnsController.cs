using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using BhavenaKhadiBhavan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace BhavenaKhadiBhavan.Controllers
{
    public class ReturnsController : Controller
    {
        private readonly IReturnService _returnService;
        private readonly ISalesService _salesService;
        private readonly IProductService _productService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReturnsController> _logger;

        public ReturnsController(
            IReturnService returnService,
            ISalesService salesService,
            IProductService productService,
            ApplicationDbContext context,
            ILogger<ReturnsController> logger)
        {
            _returnService = returnService;
            _salesService = salesService;
            _productService = productService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display returns list with filtering and search
        /// </summary>
        public async Task<IActionResult> Index(
            DateTime? fromDate,
            DateTime? toDate,
            string? search,
            string? status)
        {
            try
            {
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var returns = await _returnService.GetReturnsAsync(fromDate, toDate, search, status);

                var viewModel = new ReturnsIndexViewModel
                {
                    Returns = returns,
                    FromDate = fromDate,
                    ToDate = toDate,
                    SearchTerm = search,
                    StatusFilter = status,
                    TotalReturns = returns.Count,
                    TotalRefunds = returns.Sum(r => r.RefundAmount),
                    AverageRefundAmount = returns.Count > 0 ? returns.Average(r => r.RefundAmount) : 0
                };

                ViewBag.ReturnStatuses = ReturnStatus.GetAllStatuses();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading returns index");
                TempData["Error"] = "Error loading returns: " + ex.Message;
                return View(new ReturnsIndexViewModel());
            }
        }

        /// <summary>
        /// Show return details
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var returnTransaction = await _returnService.GetReturnByIdAsync(id);
                if (returnTransaction == null)
                {
                    TempData["Error"] = "Return not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(returnTransaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading return details for return {ReturnId}", id);
                TempData["Error"] = "Error loading return details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Create new return - GET
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(int? saleId, int? itemId)
        {
            try
            {
                if (!saleId.HasValue)
                {
                    TempData["Error"] = "Sale ID is required to create a return.";
                    return RedirectToAction("Index", "Sales");
                }

                var sale = await _salesService.GetSaleByIdAsync(saleId.Value);
                if (sale == null)
                {
                    TempData["Error"] = "Sale not found.";
                    return RedirectToAction("Index", "Sales");
                }

                var returnableItems = await _returnService.GetReturnableItemsAsync(saleId.Value);
                if (!returnableItems.Any())
                {
                    TempData["Warning"] = "No items can be returned for this sale.";
                    return RedirectToAction("Details", "Sales", new { id = saleId.Value });
                }

                var viewModel = new CreateReturnViewModel
                {
                    Sale = sale,
                    ReturnableItems = returnableItems,
                    Return = new Return
                    {
                        SaleId = saleId.Value,
                        ReturnDate = DateTime.Now,
                        Status = ReturnStatus.Pending,
                        RefundMethod = "Cash"
                    }
                };

                // Pre-select specific item if provided
                if (itemId.HasValue)
                {
                    var specificItem = returnableItems.FirstOrDefault(r => r.SaleItemId == itemId.Value);
                    if (specificItem != null)
                    {
                        var returnItem = new ReturnItemViewModel
                        {
                            SaleItemId = specificItem.SaleItemId,
                            ProductId = specificItem.ProductId,
                            ProductName = specificItem.ProductName,
                            ReturnQuantity = specificItem.ReturnableQuantity,
                            UnitPrice = specificItem.UnitPrice,
                            GSTRate = specificItem.GSTRate,
                            UnitOfMeasure = specificItem.UnitOfMeasure,
                            OriginalItemDiscountPercentage = specificItem.OriginalItemDiscountPercentage,
                            ProportionalDiscountAmount = specificItem.MaxProportionalDiscount,
                            Condition = ItemCondition.Good
                        };

                        viewModel.SelectedItems.Add(returnItem);
                        CalculateReturnTotals(viewModel);
                    }
                }

                ViewBag.ReturnReasons = ReturnReasons.GetAllReasons();
                ViewBag.ItemConditions = ItemCondition.GetAllConditions();
                ViewBag.RefundMethods = new[] { "Cash", "Card", "Store Credit", "Bank Transfer" };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading return creation page for sale {SaleId}", saleId);
                TempData["Error"] = "Error loading return page: " + ex.Message;
                return RedirectToAction("Index", "Sales");
            }
        }

        /// <summary>
        /// Process return creation - POST
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReturnViewModel model)
        {
            _logger.LogInformation("Processing return creation for sale {SaleId} with {ItemCount} items",
                model.Return.SaleId, model.SelectedItems?.Count ?? 0);

            try
            {
                // Validate basic model
                if (model.SelectedItems == null || !model.SelectedItems.Any())
                {
                    TempData["Error"] = "Please select at least one item to return.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Return.Reason))
                {
                    TempData["Error"] = "Please provide a reason for the return.";
                    return RedirectToAction(nameof(Index));
                }

                // Get available quantities for validation
                var availableQuantities = await _returnService.GetAvailableQuantitiesForReturnAsync(model.Return.SaleId);

                // Validate return quantities
                var validationErrors = ReturnCalculator.ValidateReturnQuantities(model.SelectedItems, availableQuantities);
                if (validationErrors.Any())
                {
                    foreach (var error in validationErrors)
                    {
                        TempData["Error"] = $"Item validation failed: {error.Value}";
                    }
                    return RedirectToAction(nameof(Index));
                }

                // Calculate proportional discounts
                await CalculateProportionalDiscounts(model);

                // Calculate totals
                CalculateReturnTotals(model);

                // Create return transaction
                var returnTransaction = await _returnService.CreateReturnAsync(model.Return, model.SelectedItems);

                _logger.LogInformation("Return created successfully: {ReturnNumber} for amount ₹{Amount}",
                    returnTransaction.ReturnNumber, returnTransaction.RefundAmount);

                TempData["Success"] = $"Return {returnTransaction.ReturnNumber} created successfully! " +
                    $"Refund Amount: ₹{returnTransaction.RefundAmount:N2}";

                return RedirectToAction(nameof(Details), new { id = returnTransaction.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating return for sale {SaleId}", model.Return.SaleId);
                TempData["Error"] = "Error creating return: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Process return (approve and refund) - GET
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Process(int id)
        {
            try
            {
                var returnTransaction = await _returnService.GetReturnByIdAsync(id);
                if (returnTransaction == null)
                {
                    TempData["Error"] = "Return not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (!returnTransaction.CanBeProcessed)
                {
                    TempData["Warning"] = $"Return cannot be processed. Current status: {returnTransaction.Status}";
                    return RedirectToAction(nameof(Details), new { id });
                }

                ViewBag.RefundMethods = new[] { "Cash", "Card", "Store Credit", "Bank Transfer" };
                return View(returnTransaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading return processing page for return {ReturnId}", id);
                TempData["Error"] = "Error loading return processing page: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Process return (approve and refund) - POST
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int id, string refundMethod, string? refundReference, string? notes)
        {
            try
            {
                var result = await _returnService.ProcessReturnAsync(id, refundMethod, refundReference, notes, User.Identity?.Name);

                if (result.Success)
                {
                    TempData["Success"] = $"Return processed successfully! Refund of ₹{result.RefundAmount:N2} has been issued.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                else
                {
                    TempData["Error"] = result.ErrorMessage;
                    return RedirectToAction(nameof(Process), new { id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing return {ReturnId}", id);
                TempData["Error"] = "Error processing return: " + ex.Message;
                return RedirectToAction(nameof(Process), new { id });
            }
        }

        /// <summary>
        /// Cancel return - POST
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            try
            {
                var result = await _returnService.CancelReturnAsync(id, reason, User.Identity?.Name);

                if (result.Success)
                {
                    TempData["Success"] = "Return cancelled successfully.";
                }
                else
                {
                    TempData["Error"] = result.ErrorMessage;
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling return {ReturnId}", id);
                TempData["Error"] = "Error cancelling return: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// AJAX: Get returnable items for a sale
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReturnableItems(int saleId)
        {
            try
            {
                var items = await _returnService.GetReturnableItemsAsync(saleId);
                return Json(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading returnable items for sale {SaleId}", saleId);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// AJAX: Calculate return totals
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CalculateReturnTotals([FromBody] List<ReturnItemViewModel> items)
        {
            try
            {
                if (items == null || !items.Any())
                {
                    return Json(new { subtotal = 0, discounts = 0, gst = 0, total = 0 });
                }

                // Calculate proportional discounts if not already set
                foreach (var item in items)
                {
                    if (item.ProportionalDiscountAmount == 0 && item.OriginalItemDiscountPercentage > 0)
                    {
                        var originalSubtotal = item.UnitPrice * item.ReturnQuantity;
                        item.ProportionalDiscountAmount = originalSubtotal * item.OriginalItemDiscountPercentage / 100;
                    }
                }

                var totals = ReturnCalculator.CalculateReturnTotals(items);

                return Json(new
                {
                    subtotal = totals.subtotal,
                    discounts = totals.totalDiscounts,
                    gst = totals.totalGST,
                    total = totals.refundAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating return totals");
                return Json(new { error = ex.Message });
            }
        }

        // Private helper methods

        private async Task<CreateReturnViewModel> ReloadCreateView(CreateReturnViewModel model)
        {
            try
            {
                var sale = await _salesService.GetSaleByIdAsync(model.Return.SaleId);
                var returnableItems = await _returnService.GetReturnableItemsAsync(model.Return.SaleId);

                model.Sale = sale;
                model.ReturnableItems = returnableItems;

                ViewBag.ReturnReasons = ReturnReasons.GetAllReasons();
                ViewBag.ItemConditions = ItemCondition.GetAllConditions();
                ViewBag.RefundMethods = new[] { "Cash", "Card", "Store Credit", "Bank Transfer" };

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading create view");
                throw;
            }
        }

        private async Task CalculateProportionalDiscounts(CreateReturnViewModel model)
        {
            try
            {
                // Get original sale items with discount information
                var saleItems = await _context.SaleItems
                    .Where(si => si.SaleId == model.Return.SaleId)
                    .ToListAsync();

                foreach (var returnItem in model.SelectedItems)
                {
                    var originalSaleItem = saleItems.FirstOrDefault(si => si.Id == returnItem.SaleItemId);
                    if (originalSaleItem != null)
                    {
                        // Calculate proportional discount based on return quantity
                        returnItem.ProportionalDiscountAmount = ReturnCalculator.CalculateProportionalDiscount(
                            originalSaleItem.Quantity,
                            returnItem.ReturnQuantity,
                            originalSaleItem.ItemDiscountAmount
                        );

                        returnItem.OriginalItemDiscountPercentage = originalSaleItem.ItemDiscountPercentage;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating proportional discounts");
                throw;
            }
        }

        private void CalculateReturnTotals(CreateReturnViewModel model)
        {
            var totals = ReturnCalculator.CalculateReturnTotals(model.SelectedItems);

            model.Return.SubTotal = totals.subtotal;
            model.Return.TotalItemDiscounts = totals.totalDiscounts;
            model.Return.GSTAmount = totals.totalGST;
            model.Return.RefundAmount = totals.refundAmount;

            model.TotalRefundAmount = totals.refundAmount;
            model.TotalItemDiscounts = totals.totalDiscounts;
            model.TotalGSTAmount = totals.totalGST;
        }
    }
}
