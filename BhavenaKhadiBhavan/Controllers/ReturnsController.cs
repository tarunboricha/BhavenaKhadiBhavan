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
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReturnsController> _logger;

        public ReturnsController(
            IReturnService returnService,
            ISalesService salesService,
            ApplicationDbContext context,
            ILogger<ReturnsController> logger)
        {
            _returnService = returnService;
            _salesService = salesService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display returns list with filtering
        /// </summary>
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? search)
        {
            try
            {
                // Default to last 30 days if no dates provided
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var returns = await _returnService.GetReturnsAsync(fromDate, toDate);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    returns = returns.Where(r =>
                        r.ReturnNumber.ToLower().Contains(search) ||
                        r.Sale.InvoiceNumber.ToLower().Contains(search) ||
                        r.CustomerName.ToLower().Contains(search) ||
                        (r.Sale.CustomerPhone != null && r.Sale.CustomerPhone.Contains(search))).ToList();
                }

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
                ViewBag.Search = search;
                ViewBag.TotalReturns = returns.Sum(r => r.TotalAmount);
                ViewBag.TotalCount = returns.Count;

                return View(returns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading returns index");
                TempData["Error"] = "Error loading returns: " + ex.Message;
                return View(new List<Return>());
            }
        }

        /// <summary>
        /// Show return details
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var returnEntity = await _returnService.GetReturnByIdAsync(id);
                if (returnEntity == null)
                {
                    TempData["Error"] = "Return not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(returnEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading return details for {ReturnId}", id);
                TempData["Error"] = "Error loading return details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// CRITICAL FIX: Create return from sale - GET
        /// Now properly handles partial quantities
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(int? saleId)
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

                // Check if sale can be returned
                if (sale.Status != "Completed")
                {
                    TempData["Error"] = "Only completed sales can be returned.";
                    return RedirectToAction("Details", "Sales", new { id = saleId });
                }

                // CRITICAL FIX: Get returnable items with proper quantity handling
                var returnableItems = await _returnService.GetReturnableItemsAsync(saleId.Value);
                if (!returnableItems.Any())
                {
                    TempData["Warning"] = "No items are available for return from this sale.";
                    return RedirectToAction("Details", "Sales", new { id = saleId });
                }

                ViewBag.Sale = sale;
                ViewBag.ReturnableItems = returnableItems;

                // Load return reasons
                ViewBag.ReturnReasons = new List<SelectListItem>
                {
                    new() { Value = "", Text = "Select Return Reason" },
                    new() { Value = "Defective Product", Text = "Defective Product" },
                    new() { Value = "Wrong Size", Text = "Wrong Size" },
                    new() { Value = "Wrong Color", Text = "Wrong Color" },
                    new() { Value = "Customer Changed Mind", Text = "Customer Changed Mind" },
                    new() { Value = "Damaged in Transit", Text = "Damaged in Transit" },
                    new() { Value = "Not as Described", Text = "Not as Described" },
                    new() { Value = "Quality Issue", Text = "Quality Issue" },
                    new() { Value = "Duplicate Purchase", Text = "Duplicate Purchase" },
                    new() { Value = "Other", Text = "Other" }
                };

                var returnModel = new Return
                {
                    SaleId = saleId.Value,
                    ReturnDate = DateTime.Now,
                    Status = "Completed"
                };

                return View(returnModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading return creation page for sale {SaleId}", saleId);
                TempData["Error"] = "Error loading return page: " + ex.Message;
                return RedirectToAction("Index", "Sales");
            }
        }

        /// <summary>
        /// CRITICAL FIX: Process return creation - POST
        /// Now properly handles individual item quantities
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Return returnEntity, Dictionary<int, decimal> returnQuantities, string returnReason, string? notes)
        {
            try
            {
                _logger.LogInformation("Processing return creation for sale {SaleId}", returnEntity.SaleId);

                // Validate basic input
                if (returnQuantities == null || !returnQuantities.Any(rq => rq.Value > 0))
                {
                    TempData["Error"] = "Please select at least one item with quantity to return.";
                    return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                }

                if (string.IsNullOrWhiteSpace(returnReason))
                {
                    TempData["Error"] = "Please select a return reason.";
                    return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                }

                // Set return details
                returnEntity.Reason = returnReason;
                returnEntity.Notes = notes;

                // CRITICAL FIX: Validate return quantities
                if (!await _returnService.ValidateReturnQuantitiesAsync(returnEntity.SaleId, returnQuantities))
                {
                    TempData["Error"] = "Invalid return quantities. Please check the quantities and try again.";
                    return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                }

                // Get sale and returnable items for creating return items
                var sale = await _salesService.GetSaleByIdAsync(returnEntity.SaleId);
                var returnableItems = await _returnService.GetReturnableItemsAsync(returnEntity.SaleId);

                // Create return items from selected quantities
                var returnItems = new List<ReturnItem>();

                foreach (var kvp in returnQuantities)
                {
                    var saleItemId = kvp.Key;
                    var returnQuantity = kvp.Value;

                    if (returnQuantity <= 0) continue;

                    var returnableItem = returnableItems.FirstOrDefault(ri => ri.SaleItemId == saleItemId);
                    if (returnableItem == null)
                    {
                        _logger.LogWarning("Returnable item not found for sale item {SaleItemId}", saleItemId);
                        continue;
                    }

                    // Validate quantity doesn't exceed returnable amount
                    if (returnQuantity > returnableItem.ReturnableQuantity)
                    {
                        TempData["Error"] = $"Cannot return {returnQuantity:0.###} {returnableItem.UnitOfMeasure} of {returnableItem.ProductName}. Maximum returnable: {returnableItem.ReturnableQuantity:0.###}";
                        return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                    }

                    var returnItem = new ReturnItem
                    {
                        SaleItemId = saleItemId,
                        ProductId = returnableItem.ProductId,
                        ProductName = returnableItem.ProductName,
                        ReturnQuantity = returnQuantity,
                        UnitPrice = returnableItem.UnitPrice,
                        GSTRate = returnableItem.GSTRate,
                        UnitOfMeasure = returnableItem.UnitOfMeasure
                    };

                    returnItems.Add(returnItem);
                }

                if (!returnItems.Any())
                {
                    TempData["Error"] = "No valid return items found.";
                    return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                }

                // Create the return
                var createdReturn = await _returnService.CreateReturnAsync(returnEntity, returnItems);

                _logger.LogInformation("Return {ReturnNumber} created successfully with {ItemCount} items for total ₹{TotalAmount}",
                    createdReturn.ReturnNumber, returnItems.Count, createdReturn.TotalAmount);

                TempData["Success"] = $"Return {createdReturn.ReturnNumber} created successfully! Refund Amount: ₹{createdReturn.TotalAmount:N2}";
                return RedirectToAction(nameof(Details), new { id = createdReturn.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating return for sale {SaleId}", returnEntity.SaleId);
                TempData["Error"] = "Error creating return: " + ex.Message;
                return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
            }
        }

        /// <summary>
        /// Search sales for return processing
        /// </summary>
        public IActionResult SearchSale()
        {
            return View();
        }

        /// <summary>
        /// AJAX endpoint to search sales by invoice number or customer
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchSales(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<object>());
                }

                // Search in recent sales (last 60 days for better coverage)
                var fromDate = DateTime.Today.AddDays(-60);
                var sales = await _salesService.GetSalesAsync(fromDate, DateTime.Today);

                var filteredSales = sales.Where(s =>
                    s.Status == "Completed" && (
                    s.InvoiceNumber.ToLower().Contains(term.ToLower()) ||
                    s.CustomerDisplayName.ToLower().Contains(term.ToLower()) ||
                    s.CustomerPhone?.Contains(term) == true
                ))
                .Take(10)
                .Select(s => new
                {
                    id = s.Id,
                    invoiceNumber = s.InvoiceNumber,
                    saleDate = s.SaleDate.ToString("dd/MM/yyyy"),
                    customerName = s.CustomerDisplayName,
                    customerPhone = s.CustomerPhone ?? "",
                    totalAmount = s.TotalAmount,
                    itemCount = s.ItemCount,
                    displayText = $"{s.InvoiceNumber} - {s.CustomerDisplayName} (₹{s.TotalAmount:N2})"
                })
                .ToList();

                return Json(filteredSales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching sales for term: {SearchTerm}", term);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ENHANCED: Get sale details for return processing with proper returnable quantities
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSaleForReturn(int saleId)
        {
            try
            {
                var sale = await _salesService.GetSaleByIdAsync(saleId);
                if (sale == null)
                    return Json(new { error = "Sale not found" });

                if (sale.Status != "Completed")
                    return Json(new { error = "Only completed sales can be returned" });

                // Get returnable items with proper quantity information
                var returnableItems = await _returnService.GetReturnableItemsAsync(saleId);
                if (!returnableItems.Any())
                    return Json(new { error = "No items are available for return from this sale" });

                var saleData = new
                {
                    id = sale.Id,
                    invoiceNumber = sale.InvoiceNumber,
                    saleDate = sale.SaleDate.ToString("dd/MM/yyyy HH:mm"),
                    customerName = sale.CustomerDisplayName,
                    customerPhone = sale.CustomerPhone ?? "",
                    totalAmount = sale.TotalAmount,
                    discountPercentage = sale.DiscountPercentage,
                    items = returnableItems.Select(ri => new
                    {
                        saleItemId = ri.SaleItemId,
                        productId = ri.ProductId,
                        productName = ri.ProductName,
                        originalQuantity = ri.OriginalQuantity,
                        returnedQuantity = ri.AlreadyReturnedQuantity,
                        returnableQuantity = ri.ReturnableQuantity,
                        unitPrice = ri.UnitPrice,
                        gstRate = ri.GSTRate,
                        unitOfMeasure = ri.UnitOfMeasure,
                        canBeReturned = ri.CanBeReturned,
                        displayText = ri.DisplayText
                    }).ToList()
                };

                return Json(saleData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sale for return: {SaleId}", saleId);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ENHANCED: Calculate return preview with accurate calculations
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CalculateReturnPreview(int saleId, Dictionary<int, decimal> returnQuantities)
        {
            try
            {
                if (returnQuantities == null || !returnQuantities.Any(rq => rq.Value > 0))
                {
                    return Json(new { error = "No return quantities provided" });
                }

                // Validate quantities
                if (!await _returnService.ValidateReturnQuantitiesAsync(saleId, returnQuantities))
                {
                    return Json(new { error = "Invalid return quantities" });
                }

                // Calculate return totals
                var calculation = await _returnService.CalculateReturnTotalsAsync(saleId, returnQuantities);

                var response = new
                {
                    success = true,
                    items = calculation.Items.Select(item => new
                    {
                        productName = item.ProductName,
                        returnQuantity = item.ReturnQuantity,
                        unitOfMeasure = item.UnitOfMeasure,
                        unitPrice = item.UnitPrice,
                        subtotal = item.LineSubtotal,
                        gstAmount = item.LineGST,
                        discountAmount = item.LineDiscount,
                        refundAmount = item.LineTotal
                    }).ToList(),
                    summary = new
                    {
                        subtotal = calculation.SubTotal,
                        gstAmount = calculation.GSTAmount,
                        discountAmount = calculation.DiscountAmount,
                        totalRefund = calculation.TotalAmount
                    }
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating return preview for sale {SaleId}", saleId);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Print return receipt
        /// </summary>
        public async Task<IActionResult> PrintReceipt(int id)
        {
            try
            {
                var returnEntity = await _returnService.GetReturnByIdAsync(id);
                if (returnEntity == null)
                {
                    TempData["Error"] = "Return not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Load store settings for receipt
                var storeSettings = await _context.Settings
                    .Where(s => s.Category == "Store")
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                ViewBag.StoreSettings = storeSettings;
                return View(returnEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading return receipt for {ReturnId}", id);
                TempData["Error"] = "Error loading return receipt: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Process return (mark as completed)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessReturn(int id)
        {
            try
            {
                var success = await _returnService.ProcessReturnAsync(id);
                if (success)
                {
                    TempData["Success"] = "Return processed successfully.";
                }
                else
                {
                    TempData["Error"] = "Failed to process return.";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing return {ReturnId}", id);
                TempData["Error"] = "Error processing return: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}
