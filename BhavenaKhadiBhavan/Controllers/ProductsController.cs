using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KhadiStore.Controllers
{
    /// <summary>
    /// Products controller for product management and AJAX endpoints
    /// </summary>
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupedProductsForSales(int categoryId = 0)
        {
            try
            {
                var query = _context.Products
                    .Where(p => p.IsActive)
                    .AsQueryable();

                // Filter by category if specified
                if (categoryId > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId);
                }

                var products = await query
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name)
                    .ThenBy(p => p.Size)
                    .ThenBy(p => p.Color)
                    .ToListAsync();

                // Group products by name (ignoring size and color variations)
                var groupedProducts = products
                    .GroupBy(p => new {
                        BaseName = GetBaseName(p.Name),
                        p.CategoryId,
                        p.FabricType,
                        p.SalePrice,
                        p.GSTRate
                    })
                    .Select(g => new
                    {
                        baseName = g.Key.BaseName,
                        categoryId = g.Key.CategoryId,
                        category = g.First().Category?.Name ?? "",
                        fabricType = g.Key.FabricType ?? "",
                        price = g.Key.SalePrice,
                        gstRate = g.Key.GSTRate,
                        priceWithGST = g.Key.SalePrice + (g.Key.SalePrice * g.Key.GSTRate / 100),
                        unitOfMeasure = g.First().UnitOfMeasure ?? "Piece",

                        // All variants of this product
                        variants = g.Select(p => new
                        {
                            id = p.Id,
                            fullName = p.Name,
                            size = p.Size ?? "",
                            color = p.Color ?? "",
                            stock = p.StockQuantity,
                            isLowStock = p.IsLowStock,
                            canSell = p.IsActive && p.StockQuantity > 0,
                            displayVariant = GetVariantDisplay(p.Size, p.Color)
                        }).OrderBy(v => v.size).ThenBy(v => v.color).ToList(),

                        // Summary information
                        totalStock = g.Sum(p => p.StockQuantity),
                        totalVariants = g.Count(),
                        hasStock = g.Any(p => p.StockQuantity > 0),
                        minStock = g.Min(p => p.StockQuantity),
                        maxStock = g.Max(p => p.StockQuantity)
                    })
                    .Where(g => g.hasStock) // Only show products that have stock in at least one variant
                    .OrderBy(g => g.baseName)
                    .ToList();

                return Json(groupedProducts);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

                if (product == null)
                {
                    return Json(new { error = "Product not found" });
                }

                var productDetails = new
                {
                    id = product.Id,
                    name = product.Name,
                    categoryId = product.CategoryId,
                    category = product.Category?.Name ?? "",
                    price = product.SalePrice,
                    stock = product.StockQuantity,
                    gstRate = product.GSTRate,
                    priceWithGST = product.PriceWithGST,
                    unitOfMeasure = product.UnitOfMeasure ?? "Piece",
                    size = product.Size ?? "",
                    color = product.Color ?? "",
                    fabricType = product.FabricType ?? "",
                    canSell = product.IsActive && product.StockQuantity > 0,
                    isLowStock = product.IsLowStock,
                    displayName = product.DisplayName
                };

                return Json(productDetails);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Helper methods for product grouping
        private string GetBaseName(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return "";

            // Remove common size indicators from product names for grouping
            var sizePatterns = new[] { " - S", " - M", " - L", " - XL", " - XXL", " - XXXL",
                                     " (S)", " (M)", " (L)", " (XL)", " (XXL)", " (XXXL)",
                                     " Small", " Medium", " Large", " Extra Large" };

            // Remove color indicators
            var colorPatterns = new[] { " - White", " - Black", " - Blue", " - Red", " - Green",
                                      " - Yellow", " - Pink", " - Grey", " - Brown" };

            string baseName = productName.Trim();

            // Remove size patterns
            foreach (var pattern in sizePatterns)
            {
                if (baseName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - pattern.Length).Trim();
                }
            }

            // Remove color patterns
            foreach (var pattern in colorPatterns)
            {
                if (baseName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - pattern.Length).Trim();
                }
            }

            return baseName;
        }

        private string GetVariantDisplay(string? size, string? color)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(size))
                parts.Add($"Size: {size}");

            if (!string.IsNullOrEmpty(color))
                parts.Add($"Color: {color}");

            return parts.Count > 0 ? string.Join(", ", parts) : "Standard";
        }

        /// <summary>
        /// AJAX endpoint to get products by category for sales page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProductsByCategory(int categoryId)
        {
            try
            {
                var query = _context.Products
                    .Where(p => p.IsActive)
                    .AsQueryable();

                // Filter by category if specified
                if (categoryId > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId);
                }

                var products = await query
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                // Create simple DTOs without navigation properties to avoid circular references
                var productDtos = products.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    categoryId = p.CategoryId,
                    category = p.Category?.Name ?? "",
                    price = p.SalePrice,
                    stock = p.StockQuantity,
                    gstRate = p.GSTRate,
                    priceWithGST = p.PriceWithGST,
                    canSell = p.IsActive && p.StockQuantity > 0,
                    color = p.Color ?? "",
                    size = p.Size ?? "",
                    fabricType = p.FabricType ?? ""
                }).ToList();

                return Json(productDtos);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Display products list
        /// </summary>
        public async Task<IActionResult> Index(string search, int? categoryId, string stockStatus)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(search) ||
                        p.SKU.ToLower().Contains(search) ||
                        p.Color.ToLower().Contains(search) ||
                        p.FabricType.ToLower().Contains(search));
                }

                if (categoryId.HasValue && categoryId > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(stockStatus))
                {
                    switch (stockStatus.ToLower())
                    {
                        case "instock":
                            query = query.Where(p => p.StockQuantity > p.MinimumStock);
                            break;
                        case "lowstock":
                            query = query.Where(p => p.StockQuantity <= p.MinimumStock && p.StockQuantity > 0);
                            break;
                        case "outofstock":
                            query = query.Where(p => p.StockQuantity == 0);
                            break;
                    }
                }

                var products = await query
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                ViewBag.Categories = new SelectList(categories, "Id", "Name", categoryId);
                ViewBag.CurrentSearch = search;
                ViewBag.CurrentCategory = categoryId;
                ViewBag.CurrentStockStatus = stockStatus;

                return View(products);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading products: " + ex.Message;
                return View(new List<Product>());
            }
        }

        /// <summary>
        /// Show product details
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Show create product form
        /// </summary>
        public async Task<IActionResult> Create()
        {
            try
            {
                await LoadProductFormViewBags();
                return View(new Product());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading product form: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle create product form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate SKU
                    if (!string.IsNullOrEmpty(product.SKU))
                    {
                        var existingSKU = await _context.Products
                            .FirstOrDefaultAsync(p => p.SKU.ToLower() == product.SKU.ToLower() && p.IsActive);

                        if (existingSKU != null)
                        {
                            ModelState.AddModelError("SKU", "A product with this SKU already exists.");
                            await LoadProductFormViewBags();
                            return View(product);
                        }
                    }

                    // Business validation
                    if (product.SalePrice <= product.PurchasePrice)
                    {
                        ModelState.AddModelError("SalePrice", "Sale price must be greater than purchase price.");
                        await LoadProductFormViewBags();
                        return View(product);
                    }

                    product.CreatedAt = DateTime.Now;
                    product.UpdatedAt = DateTime.Now;

                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Product '{product.Name}' created successfully!";
                    return RedirectToAction(nameof(Details), new { id = product.Id });
                }

                await LoadProductFormViewBags();
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating product: " + ex.Message;
                await LoadProductFormViewBags();
                return View(product);
            }
        }

        /// <summary>
        /// Show edit product form
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                await LoadProductFormViewBags();
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle edit product form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id)
            {
                TempData["Error"] = "Invalid product ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate SKU
                    if (!string.IsNullOrEmpty(product.SKU))
                    {
                        var existingSKU = await _context.Products
                            .FirstOrDefaultAsync(p => p.SKU.ToLower() == product.SKU.ToLower() &&
                                               p.Id != id && p.IsActive);

                        if (existingSKU != null)
                        {
                            ModelState.AddModelError("SKU", "A product with this SKU already exists.");
                            await LoadProductFormViewBags();
                            return View(product);
                        }
                    }

                    // Business validation
                    if (product.SalePrice <= product.PurchasePrice)
                    {
                        ModelState.AddModelError("SalePrice", "Sale price must be greater than purchase price.");
                        await LoadProductFormViewBags();
                        return View(product);
                    }

                    product.UpdatedAt = DateTime.Now;
                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Product '{product.Name}' updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = product.Id });
                }

                await LoadProductFormViewBags();
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating product: " + ex.Message;
                await LoadProductFormViewBags();
                return View(product);
            }
        }

        /// <summary>
        /// Show low stock products
        /// </summary>
        public async Task<IActionResult> LowStock()
        {
            try
            {
                var lowStockProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock)
                    .OrderBy(p => p.StockQuantity)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                return View(lowStockProducts);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading low stock products: " + ex.Message;
                return View(new List<Product>());
            }
        }

        /// <summary>
        /// Show stock adjustment form
        /// </summary>
        public async Task<IActionResult> StockAdjustment(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Process stock adjustment
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockAdjustment(int id, int newStock, string reason)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (newStock < 0)
                {
                    TempData["Error"] = "Stock quantity cannot be negative.";
                    return RedirectToAction(nameof(StockAdjustment), new { id });
                }

                var oldStock = product.StockQuantity;
                product.StockQuantity = newStock;
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Stock adjusted for '{product.Name}' from {oldStock} to {newStock}";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error adjusting stock: " + ex.Message;
                return RedirectToAction(nameof(StockAdjustment), new { id });
            }
        }

        // Helper methods
        private async Task LoadProductFormViewBags()
        {
            // Load categories
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = new SelectList(categories, "Id", "Name");

            // Fabric types
            ViewBag.FabricTypes = new List<SelectListItem>
            {
                new() { Value = "", Text = "Select Fabric Type" },
                new() { Value = "Cotton Khadi", Text = "Cotton Khadi" },
                new() { Value = "Silk Khadi", Text = "Silk Khadi" },
                new() { Value = "Wool Khadi", Text = "Wool Khadi" },
                new() { Value = "Cotton Silk", Text = "Cotton Silk" },
                new() { Value = "Handloom Cotton", Text = "Handloom Cotton" },
                new() { Value = "Organic Cotton", Text = "Organic Cotton" },
                new() { Value = "Linen", Text = "Linen" },
                new() { Value = "Jute", Text = "Jute" }
            };

            // Sizes
            ViewBag.Sizes = new List<SelectListItem>
            {
                new() { Value = "", Text = "Select Size" },
                new() { Value = "XS", Text = "XS" },
                new() { Value = "S", Text = "S" },
                new() { Value = "M", Text = "M" },
                new() { Value = "L", Text = "L" },
                new() { Value = "XL", Text = "XL" },
                new() { Value = "XXL", Text = "XXL" },
                new() { Value = "XXXL", Text = "XXXL" },
                new() { Value = "Free Size", Text = "Free Size" },
                new() { Value = "Per Meter", Text = "Per Meter" }
            };

            // GST Rates
            ViewBag.GSTRates = new List<SelectListItem>
            {
                new() { Value = "0", Text = "0%" },
                new() { Value = "5", Text = "5%" },
                new() { Value = "12", Text = "12%" },
                new() { Value = "18", Text = "18%" },
                new() { Value = "28", Text = "28%" }
            };
        }
    }
}