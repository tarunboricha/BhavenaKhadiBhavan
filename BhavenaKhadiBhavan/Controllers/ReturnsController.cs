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

        public ReturnsController(
            IReturnService returnService,
            ISalesService salesService,
            IProductService productService,
            ApplicationDbContext context)
        {
            _returnService = returnService;
            _salesService = salesService;
            _productService = productService;
            _context = context;
        }

        /// <summary>
        /// Display returns list
        /// </summary>
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string search)
        {
            try
            {
                // Default to last 30 days if no dates provided
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today;

                var returns = await _returnService.GetReturnsAsync(fromDate, toDate);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    returns = returns.Where(r =>
                        r.ReturnNumber.ToLower().Contains(search) ||
                        r.Sale.InvoiceNumber.ToLower().Contains(search) ||
                        r.CustomerName.ToLower().Contains(search)).ToList();
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
                TempData["Error"] = "Error loading return details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Create return from sale
        /// </summary>
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

                // Get returnable quantities
                var returnableQuantities = await _salesService.GetReturnableQuantitiesAsync(saleId.Value);
                if (!returnableQuantities.Any())
                {
                    TempData["Warning"] = "No items are available for return from this sale.";
                    return RedirectToAction("Details", "Sales", new { id = saleId });
                }

                ViewBag.Sale = sale;
                ViewBag.ReturnableQuantities = returnableQuantities;

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
                    new() { Value = "Other", Text = "Other" }
                };

                return View(new Return { SaleId = saleId.Value });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading return page: " + ex.Message;
                return RedirectToAction("Index", "Sales");
            }
        }

        /// <summary>
        /// Process return creation
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Return returnEntity, List<int> selectedItems, List<int> returnQuantities)
        {
            try
            {
                // Validate input
                if (selectedItems == null || !selectedItems.Any())
                {
                    TempData["Error"] = "Please select at least one item to return.";
                    return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                }

                if (selectedItems.Count != returnQuantities.Count)
                {
                    TempData["Error"] = "Invalid return data. Please try again.";
                    return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                }

                // Get the sale and validate
                var sale = await _salesService.GetSaleByIdAsync(returnEntity.SaleId);
                if (sale == null)
                {
                    TempData["Error"] = "Sale not found.";
                    return RedirectToAction("Index", "Sales");
                }

                // Get returnable quantities to validate
                var returnableQuantitiesDict = await _salesService.GetReturnableQuantitiesAsync(returnEntity.SaleId);

                // Create return items
                var returnItems = new List<ReturnItem>();

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    var saleItemId = selectedItems[i];
                    var returnQuantity = returnQuantities[i];

                    if (returnQuantity <= 0)
                        continue;

                    // Validate return quantity
                    if (!returnableQuantitiesDict.ContainsKey(saleItemId) ||
                        returnQuantity > returnableQuantitiesDict[saleItemId])
                    {
                        TempData["Error"] = $"Invalid return quantity for item {saleItemId}.";
                        return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                    }

                    // Get the sale item details
                    var saleItem = sale.SaleItems.FirstOrDefault(si => si.Id == saleItemId);
                    if (saleItem == null)
                    {
                        TempData["Error"] = $"Sale item {saleItemId} not found.";
                        return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
                    }

                    // Calculate proportional discount
                    var originalBillDiscountPercentage = sale.DiscountPercentage;
                    var itemSubtotal = saleItem.UnitPrice * returnQuantity;
                    var itemGST = itemSubtotal * (saleItem.GSTRate / 100);
                    var totalBeforeDiscount = itemSubtotal + itemGST;
                    var proportionalDiscount = totalBeforeDiscount * (originalBillDiscountPercentage / 100);

                    var returnItem = new ReturnItem
                    {
                        SaleItemId = saleItemId,
                        ProductId = saleItem.ProductId,
                        ProductName = saleItem.ProductName,
                        ReturnQuantity = returnQuantity,
                        UnitPrice = saleItem.UnitPrice,
                        GSTRate = saleItem.GSTRate,
                        GSTAmount = itemGST,
                        DiscountAmount = proportionalDiscount
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

                TempData["Success"] = $"Return {createdReturn.ReturnNumber} created successfully! Refund Amount: ₹{createdReturn.TotalAmount:N2}";
                return RedirectToAction(nameof(Details), new { id = createdReturn.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating return: " + ex.Message;
                return RedirectToAction(nameof(Create), new { saleId = returnEntity.SaleId });
            }
        }

        /// <summary>
        /// Search sales for return processing
        /// </summary>
        public async Task<IActionResult> SearchSale()
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

                // Search in recent sales (last 30 days)
                var fromDate = DateTime.Today.AddDays(-30);
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
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get sale details for return processing
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

                var returnableQuantities = await _salesService.GetReturnableQuantitiesAsync(saleId);
                if (!returnableQuantities.Any())
                    return Json(new { error = "No items are available for return from this sale" });

                var saleData = new
                {
                    id = sale.Id,
                    invoiceNumber = sale.InvoiceNumber,
                    saleDate = sale.SaleDate.ToString("dd/MM/yyyy HH:mm"),
                    customerName = sale.CustomerDisplayName,
                    customerPhone = sale.CustomerPhone ?? "",
                    totalAmount = sale.TotalAmount,
                    items = sale.SaleItems.Where(si => returnableQuantities.ContainsKey(si.Id))
                        .Select(si => new
                        {
                            id = si.Id,
                            productName = si.ProductName,
                            quantity = si.Quantity,
                            returnedQuantity = si.ReturnedQuantity,
                            returnableQuantity = returnableQuantities[si.Id],
                            unitPrice = si.UnitPrice,
                            gstRate = si.GSTRate,
                            lineTotal = si.LineTotal
                        }).ToList()
                };

                return Json(saleData);
            }
            catch (Exception ex)
            {
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
                TempData["Error"] = "Error loading return receipt: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Calculate return preview (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CalculateReturnPreview(int saleId, List<int> selectedItems, List<int> returnQuantities)
        {
            try
            {
                if (selectedItems == null || !selectedItems.Any() || returnQuantities == null)
                {
                    return Json(new { error = "Invalid return data" });
                }

                var sale = await _salesService.GetSaleByIdAsync(saleId);
                if (sale == null)
                {
                    return Json(new { error = "Sale not found" });
                }

                decimal totalRefund = 0;
                decimal totalGST = 0;
                decimal totalDiscount = 0;
                var returnItemsPreview = new List<object>();

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    var saleItemId = selectedItems[i];
                    var returnQuantity = returnQuantities[i];

                    if (returnQuantity <= 0) continue;

                    var saleItem = sale.SaleItems.FirstOrDefault(si => si.Id == saleItemId);
                    if (saleItem == null) continue;

                    // Calculate proportional amounts
                    var itemSubtotal = saleItem.UnitPrice * returnQuantity;
                    var itemGST = itemSubtotal * (saleItem.GSTRate / 100);
                    var totalBeforeDiscount = itemSubtotal + itemGST;
                    var proportionalDiscount = totalBeforeDiscount * (sale.DiscountPercentage / 100);
                    var refundAmount = totalBeforeDiscount - proportionalDiscount;

                    totalRefund += refundAmount;
                    totalGST += itemGST;
                    totalDiscount += proportionalDiscount;

                    returnItemsPreview.Add(new
                    {
                        productName = saleItem.ProductName,
                        returnQuantity = returnQuantity,
                        unitPrice = saleItem.UnitPrice,
                        subtotal = itemSubtotal,
                        gstAmount = itemGST,
                        discountAmount = proportionalDiscount,
                        refundAmount = refundAmount
                    });
                }

                return Json(new
                {
                    success = true,
                    items = returnItemsPreview,
                    summary = new
                    {
                        subtotal = totalRefund + totalDiscount - totalGST,
                        gstAmount = totalGST,
                        discountAmount = totalDiscount,
                        totalRefund = totalRefund
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
