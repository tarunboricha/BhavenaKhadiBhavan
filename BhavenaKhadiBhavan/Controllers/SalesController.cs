using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using BhavenaKhadiBhavan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Controllers
{
    /// <summary>
    /// Sales controller for sales process and billing
    /// </summary>
    public class SalesController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;
        private readonly ApplicationDbContext _context;

        public SalesController(
            ISalesService salesService,
            IProductService productService,
            ICustomerService customerService,
            ApplicationDbContext context)
        {
            _salesService = salesService;
            _productService = productService;
            _customerService = customerService;
            _context = context;
        }

        /// <summary>
        /// Display sales list
        /// </summary>
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string search)
        {
            try
            {
                // Default to last 30 days if no dates provided
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today;

                var sales = await _salesService.GetSalesAsync(fromDate, toDate);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    sales = sales.Where(s =>
                        s.InvoiceNumber.ToLower().Contains(search) ||
                        s.CustomerDisplayName.ToLower().Contains(search) ||
                        s.CustomerPhone?.Contains(search) == true).ToList();
                }

                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
                ViewBag.Search = search;
                ViewBag.TotalSales = sales.Sum(s => s.TotalAmount);
                ViewBag.TotalOrders = sales.Count;

                return View(sales);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading sales: " + ex.Message;
                return View(new List<Sale>());
            }
        }

        /// <summary>
        /// Show sale details
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var sale = await _salesService.GetSaleByIdAsync(id);
                if (sale == null)
                {
                    TempData["Error"] = "Sale not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Get returnable quantities for return processing
                var returnableQuantities = await _salesService.GetReturnableQuantitiesAsync(id);
                ViewBag.ReturnableQuantities = returnableQuantities;
                ViewBag.HasReturnableItems = returnableQuantities.Any();

                return View(sale);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading sale details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Create new sale - main sales interface
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(string? customerPhone)
        {
            try
            {
                var viewModel = new SalesViewModel();
                await LoadSalesViewModelAsync(viewModel);

                // Pre-fill customer if phone provided
                if (!string.IsNullOrEmpty(customerPhone))
                {
                    var customer = await _customerService.GetCustomerByPhoneAsync(customerPhone);
                    if (customer != null)
                    {
                        viewModel.Sale.CustomerName = customer.Name;
                        viewModel.Sale.CustomerPhone = customer.Phone;
                    }
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading sales page: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Process sale creation
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesViewModel model)
        {
            try
            {

                // Validate cart has items
                if (model.CartItems == null || !model.CartItems.Any())
                {
                    TempData["Error"] = "Please add items to cart before creating sale.";
                    await LoadSalesViewModelAsync(model);
                    return View(model);
                }

                // Handle customer information
                Customer? customer = null;
                if (!string.IsNullOrWhiteSpace(model.Sale.CustomerName) || !string.IsNullOrWhiteSpace(model.Sale.CustomerPhone))
                {
                    // Try to find existing customer by phone
                    if (!string.IsNullOrWhiteSpace(model.Sale.CustomerPhone))
                    {
                        customer = await _customerService.GetCustomerByPhoneAsync(model.Sale.CustomerPhone);
                    }

                    // Create new customer if not found
                    if (customer == null && (!string.IsNullOrWhiteSpace(model.Sale.CustomerName) || !string.IsNullOrWhiteSpace(model.Sale.CustomerPhone)))
                    {
                        customer = new Customer
                        {
                            Name = model.Sale.CustomerName ?? "Walk-in Customer",
                            Phone = model.Sale.CustomerPhone
                        };
                        customer = await _customerService.CreateCustomerAsync(customer);
                    }
                }

                // Set customer ID if found/created
                if (customer != null)
                {
                    model.Sale.CustomerId = customer.Id;
                }

                // CRITICAL FIX: Convert SaleItemViewModel list to SaleItem list
                var saleItems = new List<SaleItem>();
                foreach (var cartItem in model.CartItems)
                {
                    // Validate stock availability
                    var product = await _productService.GetProductByIdAsync(cartItem.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        TempData["Error"] = $"Product '{cartItem.ProductName}' is no longer available.";
                        await LoadSalesViewModelAsync(model);
                        return View(model);
                    }

                    if (product.StockQuantity < cartItem.Quantity)
                    {
                        TempData["Error"] = $"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, Required: {cartItem.Quantity}";
                        await LoadSalesViewModelAsync(model);
                        return View(model);
                    }

                    // Convert SaleItemViewModel to SaleItem
                    var saleItem = new SaleItem
                    {
                        ProductId = cartItem.ProductId,
                        ProductName = cartItem.ProductName,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.UnitPrice,
                        GSTRate = cartItem.GSTRate,
                        UnitOfMeasure = cartItem.UnitOfMeasure,
                        ItemDiscountPercentage = cartItem.ItemDiscountPercentage,
                        ItemDiscountAmount = cartItem.ItemDiscountAmount
                    };

                    // Calculate line totals with item-level discounts
                    var lineSubtotal = saleItem.UnitPrice * saleItem.Quantity;
                    var lineAfterDiscount = lineSubtotal - saleItem.ItemDiscountAmount;
                    var lineGST = lineAfterDiscount * saleItem.GSTRate / 100;
                    saleItem.GSTAmount = lineGST;
                    saleItem.LineTotal = lineAfterDiscount + lineGST;

                    saleItems.Add(saleItem);
                }

                // Validate payment method
                if (string.IsNullOrWhiteSpace(model.Sale.PaymentMethod))
                {
                    model.Sale.PaymentMethod = "Cash"; // Default to cash
                }

                // Create the sale with item-level discounts
                var sale = await _salesService.CreateSaleAsync(model.Sale, saleItems);

                TempData["Success"] = $"Sale {sale.InvoiceNumber} created successfully! " +
                    $"Total: ₹{sale.TotalAmount:N2}" +
                    (sale.DiscountAmount > 0 ? $" (Saved: ₹{sale.DiscountAmount:N2})" : "");

                return RedirectToAction(nameof(Details), new { id = sale.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating sale: " + ex.Message;
                await LoadSalesViewModelAsync(model);
                return View(model);
            }
        }

        /// <summary>
        /// AJAX endpoint to add item to cart
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null || !product.IsActive)
                {
                    return Json(new { success = false, message = "Product not found or inactive." });
                }

                if (product.StockQuantity < quantity)
                {
                    return Json(new { success = false, message = $"Insufficient stock. Available: {product.StockQuantity}" });
                }

                var cartItem = new SaleItem
                {
                    ProductId = product.Id,
                    ProductName = product.DisplayName,
                    Quantity = quantity,
                    UnitPrice = product.SalePrice,
                    GSTRate = product.GSTRate
                };

                // Store cart in session (simplified approach)
                var cart = GetCartFromSession();

                // Check if item already in cart
                var existingItem = cart.FirstOrDefault(c => c.ProductId == productId);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    if (existingItem.Quantity > product.StockQuantity)
                    {
                        return Json(new { success = false, message = $"Total quantity would exceed stock. Available: {product.StockQuantity}" });
                    }
                }
                else
                {
                    cart.Add(cartItem);
                }

                SaveCartToSession(cart);

                return Json(new
                {
                    success = true,
                    message = $"Added {quantity} x {product.DisplayName} to cart",
                    cartCount = cart.Sum(c => c.Quantity),
                    cartTotal = CalculateCartTotal(cart)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding to cart: " + ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to remove item from cart
        /// </summary>
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            try
            {
                var cart = GetCartFromSession();
                cart.RemoveAll(c => c.ProductId == productId);
                SaveCartToSession(cart);

                return Json(new
                {
                    success = true,
                    message = "Item removed from cart",
                    cartCount = cart.Sum(c => c.Quantity),
                    cartTotal = CalculateCartTotal(cart)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error removing from cart: " + ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to update cart item quantity
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateCartQuantity(int productId, int quantity)
        {
            try
            {
                if (quantity <= 0)
                {
                    return RemoveFromCart(productId);
                }

                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found." });
                }

                if (product.StockQuantity < quantity)
                {
                    return Json(new { success = false, message = $"Insufficient stock. Available: {product.StockQuantity}" });
                }

                var cart = GetCartFromSession();
                var cartItem = cart.FirstOrDefault(c => c.ProductId == productId);
                if (cartItem != null)
                {
                    cartItem.Quantity = quantity;
                    SaveCartToSession(cart);
                }

                return Json(new
                {
                    success = true,
                    cartCount = cart.Sum(c => c.Quantity),
                    cartTotal = CalculateCartTotal(cart)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating cart: " + ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to get current cart
        /// </summary>
        [HttpGet]
        public IActionResult GetCart()
        {
            try
            {
                var cart = GetCartFromSession();
                var cartSummary = CalculateCartSummary(cart);

                return Json(new
                {
                    success = true,
                    items = cart.Select(c => new
                    {
                        productId = c.ProductId,
                        productName = c.ProductName,
                        quantity = c.Quantity,
                        unitPrice = c.UnitPrice,
                        gstRate = c.GSTRate,
                        lineTotal = (c.UnitPrice * c.Quantity) + ((c.UnitPrice * c.Quantity) * c.GSTRate / 100)
                    }),
                    summary = cartSummary
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error getting cart: " + ex.Message });
            }
        }

        /// <summary>
        /// Clear cart
        /// </summary>
        [HttpPost]
        public IActionResult ClearCart()
        {
            try
            {
                SaveCartToSession(new List<SaleItem>());
                return Json(new { success = true, message = "Cart cleared" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error clearing cart: " + ex.Message });
            }
        }

        /// <summary>
        /// Print invoice
        /// </summary>
        public async Task<IActionResult> PrintInvoice(int id)
        {
            try
            {
                var sale = await _salesService.GetSaleByIdAsync(id);
                if (sale == null)
                {
                    TempData["Error"] = "Sale not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Load store settings for invoice
                var storeSettings = await _context.Settings
                    .Where(s => s.Category == "Store")
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                ViewBag.StoreSettings = storeSettings;
                return View(sale);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading invoice: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper methods
        private List<SaleItem> GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
                return new List<SaleItem>();

            return System.Text.Json.JsonSerializer.Deserialize<List<SaleItem>>(cartJson) ?? new List<SaleItem>();
        }

        private void SaveCartToSession(List<SaleItem> cart)
        {
            var cartJson = System.Text.Json.JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString("Cart", cartJson);
        }

        private decimal CalculateCartTotal(List<SaleItem> cart)
        {
            return cart.Sum(c => (c.UnitPrice * c.Quantity) + ((c.UnitPrice * c.Quantity) * c.GSTRate / 100));
        }

        private object CalculateCartSummary(List<SaleItem> cart)
        {
            var subtotal = cart.Sum(c => c.UnitPrice * c.Quantity);
            var gstAmount = cart.Sum(c => (c.UnitPrice * c.Quantity) * c.GSTRate / 100);
            var total = subtotal + gstAmount;

            return new
            {
                itemCount = cart.Sum(c => c.Quantity),
                subtotal = subtotal,
                gstAmount = gstAmount,
                total = total
            };
        }

        // CRITICAL FIX: Updated helper method to work with SaleItemViewModel
        private async Task LoadSalesViewModelAsync(SalesViewModel model)
        {
            try
            {
                // Load categories without navigation properties
                model.Categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .Select(c => new Category
                    {
                        Id = c.Id,
                        Name = c.Name,
                        IsActive = c.IsActive
                    })
                    .ToListAsync();

                // Load customers
                model.Customers = await _customerService.GetAllCustomersAsync();

                // Initialize empty cart if not provided - FIXED TYPE
                model.CartItems ??= new List<SaleItemViewModel>();

                // Calculate cart totals with item-level discounts
                var cartSummary = CalculateCartSummaryWithDiscounts(model.CartItems);
                model.CartSubtotal = cartSummary.subtotal;
                model.CartGST = cartSummary.gstAmount;
                model.CartDiscount = cartSummary.totalDiscount;
                model.CartTotal = cartSummary.total;

                // Payment method options
                ViewBag.PaymentMethods = new List<SelectListItem>
                {
                    new() { Value = "Cash", Text = "Cash", Selected = true },
                    new() { Value = "Card", Text = "Card" },
                    new() { Value = "UPI", Text = "UPI" },
                    new() { Value = "Bank Transfer", Text = "Bank Transfer" }
                };
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // CRITICAL FIX: Updated calculation method for SaleItemViewModel
        private (decimal subtotal, decimal totalDiscount, decimal gstAmount, decimal total) CalculateCartSummaryWithDiscounts(List<SaleItemViewModel> cartItems)
        {
            var subtotal = cartItems.Sum(c => c.UnitPrice * c.Quantity);
            var totalDiscount = cartItems.Sum(c => c.ItemDiscountAmount);
            var gstAmount = cartItems.Sum(c => {
                var lineAfterDiscount = (c.UnitPrice * c.Quantity) - c.ItemDiscountAmount;
                return lineAfterDiscount * c.GSTRate / 100;
            });
            var total = subtotal - totalDiscount + gstAmount;

            return (subtotal, totalDiscount, gstAmount, total);
        }
    }
}