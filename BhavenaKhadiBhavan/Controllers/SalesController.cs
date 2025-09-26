using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using BhavenaKhadiBhavan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Controllers
{
    /// <summary>
    /// Sales controller for sales process and billing - Enhanced with step-by-step product selection
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
        /// Show sale details with item-level discount information
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

                // Add discount summary
                var discountSummary = await _salesService.GetSaleDiscountSummaryAsync(id);
                ViewBag.DiscountSummary = discountSummary;

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
        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new SalesViewModel();
                await LoadSalesViewModelAsync(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading sales page: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Process sale creation with item-level discounts
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesViewModel model)
        {
            try
            {
                // Get cart from session
                var cartItems = GetCartFromSession();

                // Validate cart has items
                if (!cartItems.Any())
                {
                    TempData["Error"] = "Please add items to cart before creating sale.";
                    await LoadSalesViewModelAsync(model);
                    return View(model);
                }

                // Validate customer info if provided
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

                // Validate stock availability
                foreach (var item in cartItems)
                {
                    var product = await _productService.GetProductByIdAsync(item.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        TempData["Error"] = $"Product '{item.ProductName}' is no longer available.";
                        await LoadSalesViewModelAsync(model);
                        return View(model);
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        TempData["Error"] = $"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, Required: {item.Quantity}";
                        await LoadSalesViewModelAsync(model);
                        return View(model);
                    }
                }

                // Create sale from cart items
                var sale = await _salesService.CreateSaleFromCartAsync(model.Sale, cartItems);

                // Clear cart after successful sale
                SaveCartToSession(new List<CartItemViewModel>());

                TempData["Success"] = $"Sale {sale.InvoiceNumber} created successfully! Total: ₹{sale.TotalAmount:N2}";
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
        /// STEP 1: Get distinct product names (performance optimized)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProductNames(int? categoryId = null, string? searchTerm = null)
        {
            try
            {
                var query = _context.Products
                    .Where(p => p.IsActive && p.StockQuantity > 0);

                // Apply category filter
                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()));
                }

                // Group by product name and get summary info
                var productNames = await query
                    .GroupBy(p => p.Name)
                    .Select(g => new
                    {
                        name = g.Key,
                        categoryName = g.First().Category != null ? g.First().Category.Name : "Uncategorized",
                        minPrice = g.Min(p => p.SalePrice),
                        maxPrice = g.Max(p => p.SalePrice),
                        totalVariants = g.Count(),
                        totalStock = g.Sum(p => p.StockQuantity),
                        gstRate = g.First().GSTRate,
                        fabricType = g.First().FabricType
                    })
                    .OrderBy(pn => pn.name)
                    .ToListAsync();

                return Json(new { success = true, productNames });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading product names: " + ex.Message });
            }
        }

        /// <summary>
        /// STEP 2: Get colors for selected product name
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProductColors(string productName)
        {
            try
            {
                if (string.IsNullOrEmpty(productName))
                {
                    return Json(new { success = false, message = "Product name is required" });
                }

                var colors = await _context.Products
                    .Where(p => p.IsActive && p.Name == productName && p.StockQuantity > 0)
                    .Where(p => !string.IsNullOrEmpty(p.Color))
                    .GroupBy(p => p.Color)
                    .Select(g => new
                    {
                        color = g.Key!,
                        variantCount = g.Count(),
                        totalStock = g.Sum(p => p.StockQuantity),
                        minPrice = g.Min(p => p.SalePrice),
                        maxPrice = g.Max(p => p.SalePrice)
                    })
                    .OrderBy(c => c.color)
                    .ToListAsync();

                return Json(new { success = true, colors });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading colors: " + ex.Message });
            }
        }

        /// <summary>
        /// STEP 3: Get sizes for selected product name and color
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProductSizes(string productName, string color)
        {
            try
            {
                if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(color))
                {
                    return Json(new { success = false, message = "Product name and color are required" });
                }

                var sizes = await _context.Products
                    .Where(p => p.IsActive && p.Name == productName && p.Color == color && p.StockQuantity > 0)
                    .Select(p => new
                    {
                        productId = p.Id,
                        size = p.Size ?? "Free Size",
                        salePrice = p.SalePrice,
                        stockQuantity = p.StockQuantity,
                        gstRate = p.GSTRate,
                        unitOfMeasure = p.UnitOfMeasure ?? "Piece",
                        priceWithGST = p.SalePrice + (p.SalePrice * p.GSTRate / 100),
                        isLowStock = p.IsLowStock,
                        stockStatus = p.StockStatus,
                        displayStock = p.DisplayStock
                    })
                    .OrderBy(s => s.size)
                    .ToListAsync();

                return Json(new { success = true, sizes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading sizes: " + ex.Message });
            }
        }

        /// <summary>
        /// Add item to cart with discount support
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, decimal quantity, decimal discountPercentage = 0)
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

                var cartItem = new CartItemViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.DisplayName,
                    Quantity = quantity,
                    UnitPrice = product.SalePrice,
                    GSTRate = product.GSTRate,
                    UnitOfMeasure = product.UnitOfMeasure ?? "Piece"
                };

                // Apply discount if provided
                if (discountPercentage > 0)
                {
                    cartItem.ApplyDiscountPercentage(discountPercentage);
                }

                // Store cart in session
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

                    // Apply same discount to updated quantity
                    if (discountPercentage > 0)
                    {
                        existingItem.ApplyDiscountPercentage(discountPercentage);
                    }
                }
                else
                {
                    cart.Add(cartItem);
                }

                SaveCartToSession(cart);
                var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(cart);

                return Json(new
                {
                    success = true,
                    message = $"Added {quantity} x {product.DisplayName} to cart",
                    cartCount = cartTotals.ItemCount,
                    cartTotal = cartTotals.Total,
                    hasDiscounts = cartTotals.ItemsWithDiscounts > 0,
                    discountAmount = cartTotals.DiscountAmount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding to cart: " + ex.Message });
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            try
            {
                var cart = GetCartFromSession();
                cart.RemoveAll(c => c.ProductId == productId);
                SaveCartToSession(cart);

                var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(cart);

                return Json(new
                {
                    success = true,
                    message = "Item removed from cart",
                    cartCount = cartTotals.ItemCount,
                    cartTotal = cartTotals.Total,
                    hasDiscounts = cartTotals.ItemsWithDiscounts > 0,
                    discountAmount = cartTotals.DiscountAmount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error removing from cart: " + ex.Message });
            }
        }

        /// <summary>
        /// Update cart item quantity and discount
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateCartQuantity(int productId, decimal quantity, decimal? discountPercentage = null)
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

                    // Update discount if provided
                    if (discountPercentage.HasValue)
                    {
                        cartItem.ApplyDiscountPercentage(discountPercentage.Value);
                    }

                    SaveCartToSession(cart);
                }

                var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(cart);

                return Json(new
                {
                    success = true,
                    cartCount = cartTotals.ItemCount,
                    cartTotal = cartTotals.Total,
                    hasDiscounts = cartTotals.ItemsWithDiscounts > 0,
                    discountAmount = cartTotals.DiscountAmount,
                    itemLineTotal = cartItem?.LineTotalWithDiscount ?? 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating cart: " + ex.Message });
            }
        }

        /// <summary>
        /// Apply overall discount to all cart items
        /// </summary>
        [HttpPost]
        public IActionResult ApplyOverallDiscount(decimal discountPercentage)
        {
            try
            {
                var cart = GetCartFromSession();

                // Apply discount to all items
                foreach (var item in cart)
                {
                    item.ApplyDiscountPercentage(discountPercentage);
                }

                SaveCartToSession(cart);
                var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(cart);

                return Json(new
                {
                    success = true,
                    message = $"Applied {discountPercentage:0.##}% discount to all items",
                    cartTotal = cartTotals.Total,
                    discountAmount = cartTotals.DiscountAmount,
                    itemsWithDiscounts = cartTotals.ItemsWithDiscounts,
                    effectiveDiscountPercentage = cartTotals.EffectiveDiscountPercentage
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error applying discount: " + ex.Message });
            }
        }

        /// <summary>
        /// Apply discount to specific cart item
        /// </summary>
        [HttpPost]
        public IActionResult ApplyItemDiscount(int productId, decimal discountPercentage)
        {
            try
            {
                var cart = GetCartFromSession();
                var cartItem = cart.FirstOrDefault(c => c.ProductId == productId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Item not found in cart." });
                }

                cartItem.ApplyDiscountPercentage(discountPercentage);
                SaveCartToSession(cart);

                var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(cart);

                return Json(new
                {
                    success = true,
                    message = $"Applied {discountPercentage:0.##}% discount to {cartItem.ProductName}",
                    itemLineTotal = cartItem.LineTotalWithDiscount,
                    itemDiscountAmount = cartItem.ItemDiscountAmount,
                    cartTotal = cartTotals.Total,
                    discountAmount = cartTotals.DiscountAmount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error applying item discount: " + ex.Message });
            }
        }

        /// <summary>
        /// Get current cart with discount information
        /// </summary>
        [HttpGet]
        public IActionResult GetCart()
        {
            try
            {
                var cart = GetCartFromSession();
                var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(cart);

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
                        unitOfMeasure = c.UnitOfMeasure,
                        // Item-level discount information
                        itemDiscountPercentage = c.ItemDiscountPercentage,
                        itemDiscountAmount = c.ItemDiscountAmount,
                        lineSubtotal = c.LineSubtotal,
                        lineAfterDiscount = c.LineSubtotalAfterDiscount,
                        lineGST = c.LineGSTAmount,
                        lineTotal = c.LineTotalWithDiscount,
                        hasDiscount = c.HasDiscount
                    }),
                    summary = new
                    {
                        itemCount = cartTotals.ItemCount,
                        subtotal = cartTotals.Subtotal,
                        discountAmount = cartTotals.DiscountAmount,
                        gstAmount = cartTotals.GSTAmount,
                        total = cartTotals.Total,
                        itemsWithDiscounts = cartTotals.ItemsWithDiscounts,
                        effectiveDiscountPercentage = cartTotals.EffectiveDiscountPercentage
                    }
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
                SaveCartToSession(new List<CartItemViewModel>());
                return Json(new { success = true, message = "Cart cleared" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error clearing cart: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var validStatuses = new[] { "Pending", "Completed", "Cancelled" };
                if (!validStatuses.Contains(status))
                {
                    return Json(new { success = false, message = "Invalid status." });
                }

                var result = await _salesService.UpdateSaleStatusAsync(id, status);
                if (result)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Sale status updated to {status}.",
                        newStatus = status
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Sale not found or update failed." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
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

        // ============================================
        // Helper methods (enhanced for discounts)
        // ============================================

        private List<CartItemViewModel> GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson))
                return new List<CartItemViewModel>();

            return System.Text.Json.JsonSerializer.Deserialize<List<CartItemViewModel>>(cartJson) ?? new List<CartItemViewModel>();
        }

        private void SaveCartToSession(List<CartItemViewModel> cart)
        {
            var cartJson = System.Text.Json.JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString("Cart", cartJson);
        }

        private async Task LoadSalesViewModelAsync(SalesViewModel model)
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

            // Load products without circular references
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Include(p => p.Category)
                .ToListAsync();

            model.Products = products.Select(p => new Product
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                SalePrice = p.SalePrice,
                StockQuantity = p.StockQuantity,
                MinimumStock = p.MinimumStock,
                GSTRate = p.GSTRate,
                Color = p.Color,
                Size = p.Size,
                FabricType = p.FabricType,
                UnitOfMeasure = p.UnitOfMeasure,
                IsActive = p.IsActive,
                Category = p.Category != null ? new Category
                {
                    Id = p.Category.Id,
                    Name = p.Category.Name
                } : null
            }).ToList();

            model.Customers = await _customerService.GetAllCustomersAsync();
            model.CartItems = GetCartFromSession();

            // Calculate totals with discounts
            var cartTotals = _salesService.CalculateCartTotalsWithDiscounts(model.CartItems);
            model.CartSubtotal = cartTotals.Subtotal;
            model.CartDiscountAmount = cartTotals.DiscountAmount;
            model.CartGST = cartTotals.GSTAmount;
            model.CartTotal = cartTotals.Total;

            // Payment method options
            ViewBag.PaymentMethods = new List<SelectListItem>
            {
                new() { Value = "Cash", Text = "Cash" },
                new() { Value = "Card", Text = "Card" },
                new() { Value = "UPI", Text = "UPI" },
                new() { Value = "Bank Transfer", Text = "Bank Transfer" }
            };
        }
    }
}