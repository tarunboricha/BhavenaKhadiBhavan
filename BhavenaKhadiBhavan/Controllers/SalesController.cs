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
        /// Process sale creation
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesViewModel model)
        {
            try
            {
                // Validate cart has items
                if (!model.CartItems.Any())
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
                foreach (var item in model.CartItems)
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

                // Create the sale
                var sale = await _salesService.CreateSaleAsync(model.Sale, model.CartItems);

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

            // Load products without circular references - create simple DTOs
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Include(p => p.Category)
                .ToListAsync();

            // Create products without navigation properties for JSON serialization
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
                IsActive = p.IsActive,
                // Create a simple category object without navigation properties
                Category = p.Category != null ? new Category
                {
                    Id = p.Category.Id,
                    Name = p.Category.Name
                } : null
            }).ToList();

            model.Customers = await _customerService.GetAllCustomersAsync();
            model.CartItems = GetCartFromSession();

            var cartSummary = CalculateCartSummary(model.CartItems);
            model.CartSubtotal = ((dynamic)cartSummary).subtotal;
            model.CartGST = ((dynamic)cartSummary).gstAmount;
            model.CartTotal = ((dynamic)cartSummary).total;

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