using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    public class SalesService : ISalesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;

        public SalesService(ApplicationDbContext context, IProductService productService, ICustomerService customerService)
        {
            _context = context;
            _productService = productService;
            _customerService = customerService;
        }

        /// <summary>
        /// FIXED: Create sale with SaleItem objects (for direct use)
        /// </summary>
        public async Task<Sale> CreateSaleAsync(Sale sale, List<SaleItem> saleItems)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Generate invoice number
                sale.InvoiceNumber = await GenerateInvoiceNumberAsync();
                sale.SaleDate = DateTime.Now;

                // CRITICAL FIX: Validate and update stock atomically FIRST
                foreach (var item in saleItems)
                {
                    // Use atomic SQL update to prevent race conditions
                    var stockUpdateQuery = @"
                UPDATE Products 
                SET StockQuantity = StockQuantity - @quantity,
                    UpdatedAt = GETDATE()
                WHERE Id = @productId 
                AND StockQuantity >= @quantity 
                AND IsActive = 1";

                    var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                        stockUpdateQuery,
                        new Microsoft.Data.SqlClient.SqlParameter("@quantity", item.Quantity),
                        new Microsoft.Data.SqlClient.SqlParameter("@productId", item.ProductId));

                    if (rowsAffected == 0)
                    {
                        // Get current product info for error message
                        var product = await _context.Products
                            .Where(p => p.Id == item.ProductId)
                            .Select(p => new { p.Name, p.StockQuantity, p.IsActive })
                            .FirstOrDefaultAsync();

                        if (product == null || !product.IsActive)
                        {
                            throw new InvalidOperationException($"Product with ID {item.ProductId} not found or inactive");
                        }
                        else
                        {
                            throw new InvalidOperationException($"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, Required: {item.Quantity}");
                        }
                    }
                }

                // CRITICAL: Calculate totals with item-level discounts
                decimal subtotal = 0;
                decimal totalItemDiscounts = 0;
                decimal totalGST = 0;

                foreach (var item in saleItems)
                {
                    // Basic calculations
                    var itemSubtotal = item.UnitPrice * item.Quantity;

                    // Apply item-level discount
                    var itemDiscountAmount = item.ItemDiscountAmount;
                    var itemAfterDiscount = itemSubtotal - itemDiscountAmount;

                    // Calculate GST on discounted amount
                    var itemGST = itemAfterDiscount * item.GSTRate / 100;
                    var itemTotal = itemAfterDiscount + itemGST;

                    // Set item totals
                    item.GSTAmount = itemGST;
                    item.LineTotal = itemTotal;

                    // Accumulate sale totals
                    subtotal += itemSubtotal;
                    totalItemDiscounts += itemDiscountAmount;
                    totalGST += itemGST;
                }

                // Set sale totals (item-based calculation)
                sale.SubTotal = subtotal;
                sale.DiscountAmount = totalItemDiscounts; // Sum of item discounts
                sale.GSTAmount = totalGST;
                sale.TotalAmount = subtotal - totalItemDiscounts + totalGST;

                // Update overall discount percentage for backward compatibility
                sale.DiscountPercentage = subtotal > 0 ? (totalItemDiscounts / subtotal) * 100 : 0;

                // Add sale to context
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Add sale items with proper unit of measure
                foreach (var item in saleItems)
                {
                    item.SaleId = sale.Id;

                    // Get product info for unit of measure
                    var product = await _context.Products
                        .Where(p => p.Id == item.ProductId)
                        .Select(p => new { p.UnitOfMeasure })
                        .FirstOrDefaultAsync();

                    // Set unit of measure
                    item.UnitOfMeasure = product?.UnitOfMeasure ?? "Piece";

                    _context.SaleItems.Add(item);
                }

                await _context.SaveChangesAsync();

                // Update customer totals if customer exists
                if (sale.CustomerId.HasValue)
                {
                    await _customerService.UpdateCustomerTotalsAsync(sale.CustomerId.Value);
                }

                await transaction.CommitAsync();
                return sale;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Re-throw with more context for debugging
                throw new InvalidOperationException($"Failed to create sale: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// CRITICAL: Create sale from cart items (used by controller)
        /// </summary>
        public async Task<Sale> CreateSaleFromCartAsync(Sale sale, List<CartItemViewModel> cartItems)
        {
            // Convert cart items to sale items
            var saleItems = cartItems.Select(ci => new SaleItem
            {
                ProductId = ci.ProductId,
                ProductName = ci.ProductName,
                Quantity = ci.Quantity,
                UnitPrice = ci.UnitPrice,
                GSTRate = ci.GSTRate,
                UnitOfMeasure = ci.UnitOfMeasure,
                ItemDiscountPercentage = ci.ItemDiscountPercentage,
                ItemDiscountAmount = ci.ItemDiscountAmount
            }).ToList();

            return await CreateSaleAsync(sale, saleItems);
        }

        public async Task<Sale?> GetSaleByIdAsync(int id)
        {
            return await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
        {
            return await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.InvoiceNumber == invoiceNumber);
        }

        public async Task<List<Sale>> GetSalesByDateAsync(DateTime date)
        {
            return await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate.Date == date.Date)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<List<Sale>> GetAllSalesAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date <= toDate.Value.Date);
            }

            return await query
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<List<Sale>> GetSalesByStatusAsync(string status, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Where(s => s.Status == status)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date <= toDate.Value.Date);
            }

            return await query
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<object> GetSalesStatusSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Sales.AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date <= toDate.Value.Date);
            }

            return new
            {
                TotalSales = await query.CountAsync(),
                CompletedSales = await query.CountAsync(s => s.Status == "Completed"),
                PendingSales = await query.CountAsync(s => s.Status == "Pending"),
                CancelledSales = await query.CountAsync(s => s.Status == "Cancelled"),
                CompletedRevenue = await query.Where(s => s.Status == "Completed").SumAsync(s => s.TotalAmount),
                PendingRevenue = await query.Where(s => s.Status == "Pending").SumAsync(s => s.TotalAmount),
                CancelledRevenue = await query.Where(s => s.Status == "Cancelled").SumAsync(s => s.TotalAmount)
            };
        }

        public async Task<List<Sale>> GetSalesAsync(DateTime? fromDate = null, DateTime? toDate = null, string? status = null)
        {
            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .AsQueryable();

            // Apply date filters
            if (fromDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(s => s.SaleDate.Date <= toDate.Value.Date);
            }

            // **CRITICAL: Apply status filter (default to completed for reports)**
            if (status != null)
            {
                query = query.Where(s => s.Status == status);
            }
            else
            {
                // **DEFAULT: Only include completed sales in general queries**
                query = query.Where(s => s.Status == "Completed");
            }

            return await query
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<string> GenerateInvoiceNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"INV{today:yyyyMMdd}";

            // Use a more robust approach to prevent collisions
            var maxRetries = 10;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                var lastInvoice = await _context.Sales
                    .Where(s => s.InvoiceNumber.StartsWith(prefix))
                    .OrderByDescending(s => s.InvoiceNumber)
                    .FirstOrDefaultAsync();

                int sequence = 1;
                if (lastInvoice != null && lastInvoice.InvoiceNumber.Length > prefix.Length)
                {
                    var lastSequence = lastInvoice.InvoiceNumber.Substring(prefix.Length);
                    if (int.TryParse(lastSequence, out int lastNum))
                    {
                        sequence = lastNum + 1;
                    }
                }

                var newInvoiceNumber = $"{prefix}{sequence:D3}";

                // Check if this invoice number already exists (race condition check)
                var exists = await _context.Sales
                    .AnyAsync(s => s.InvoiceNumber == newInvoiceNumber);

                if (!exists)
                {
                    return newInvoiceNumber;
                }

                // If collision, wait briefly and retry
                await Task.Delay(50);
            }

            // Fallback: use timestamp if all retries failed
            return $"{prefix}{DateTime.Now:HHmmssff}";
        }

        public async Task<bool> SafeUpdateCustomerTotalsAsync(int customerId)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null) return false;

                // Calculate totals from completed sales only
                var salesData = await _context.Sales
                    .Where(s => s.CustomerId == customerId && s.Status == "Completed")
                    .Select(s => new { s.TotalAmount, s.SaleDate })
                    .ToListAsync();

                customer.TotalOrders = salesData.Count;
                customer.TotalPurchases = salesData.Sum(s => s.TotalAmount);
                customer.LastPurchaseDate = salesData.Any() ?
                    salesData.Max(s => s.SaleDate) : (DateTime?)null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                // Log error if needed, but don't fail the entire sale
                return false;
            }
        }

        /// <summary>
        /// ADDED: Validate stock availability before sale creation
        /// </summary>
        public async Task<(bool isValid, List<string> errors)> ValidateStockAvailabilityAsync(List<CartItemViewModel> cartItems)
        {
            var errors = new List<string>();

            foreach (var item in cartItems)
            {
                var product = await _context.Products
                    .Where(p => p.Id == item.ProductId && p.IsActive)
                    .Select(p => new { p.Name, p.StockQuantity, p.IsActive })
                    .FirstOrDefaultAsync();

                if (product == null || !product.IsActive)
                {
                    errors.Add($"Product '{item.ProductName}' is no longer available");
                }
                else if (product.StockQuantity < item.Quantity)
                {
                    errors.Add($"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, Required: {item.Quantity}");
                }
            }

            return (errors.Count == 0, errors);
        }


        public async Task<decimal> CalculateGSTAmountAsync(List<SaleItem> items)
        {
            return items.Sum(item =>
            {
                var itemSubtotal = item.UnitPrice * item.Quantity;
                var itemAfterDiscount = itemSubtotal - item.ItemDiscountAmount;
                return itemAfterDiscount * (item.GSTRate / 100);
            });
        }

        public async Task<bool> ProcessSaleCompletionAsync(int saleId)
        {
            var sale = await GetSaleByIdAsync(saleId);
            if (sale == null) return false;

            sale.Status = "Completed";
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Dictionary<int, decimal>> GetReturnableQuantitiesAsync(int saleId)
        {
            var saleItems = await _context.SaleItems
                .Where(si => si.SaleId == saleId)
                .ToListAsync();

            var returnableQuantities = new Dictionary<int, decimal>();

            foreach (var item in saleItems)
            {
                var returnedQty = await _context.ReturnItems
                    .Where(ri => ri.SaleItemId == item.Id)
                    .SumAsync(ri => ri.ReturnQuantity);

                var returnableQty = item.Quantity - returnedQty;
                if (returnableQty > 0)
                {
                    returnableQuantities[item.Id] = returnableQty;
                }
            }

            return returnableQuantities;
        }

        // =========================================
        // NEW: Item-Level Discount Methods
        // =========================================

        /// <summary>
        /// Apply overall discount percentage to all items in a sale
        /// </summary>
        public async Task<bool> ApplyOverallDiscountAsync(int saleId, decimal discountPercentage)
        {
            var sale = await GetSaleByIdAsync(saleId);
            if (sale == null) return false;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var item in sale.SaleItems)
                {
                    item.ApplyDiscountPercentage(discountPercentage);
                    item.GSTAmount = item.LineGSTAmount;
                    item.LineTotal = item.LineTotalWithDiscount;
                }

                sale.RecalculateTotals();
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> UpdateSaleStatusAsync(int saleId, string status)
        {
            try
            {
                var validStatuses = new[] { "Pending", "Completed", "Cancelled" };
                if (!validStatuses.Contains(status))
                {
                    return false;
                }

                var sale = await _context.Sales.FindAsync(saleId);
                if (sale == null)
                {
                    return false;
                }

                sale.Status = status;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Apply discount to specific item
        /// </summary>
        public async Task<DiscountOperationResult> ApplyItemDiscountAsync(int saleItemId, decimal discountPercentage)
        {
            var saleItem = await _context.SaleItems
                .Include(si => si.Sale)
                .FirstOrDefaultAsync(si => si.Id == saleItemId);

            if (saleItem == null)
            {
                return new DiscountOperationResult
                {
                    Success = false,
                    Message = "Sale item not found"
                };
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Apply discount to item
                saleItem.ApplyDiscountPercentage(discountPercentage);
                saleItem.GSTAmount = saleItem.LineGSTAmount;
                saleItem.LineTotal = saleItem.LineTotalWithDiscount;

                // Recalculate sale totals
                var sale = saleItem.Sale;
                if (sale != null)
                {
                    sale.RecalculateTotals();
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new DiscountOperationResult
                {
                    Success = true,
                    Message = "Discount applied successfully",
                    NewDiscountAmount = saleItem.ItemDiscountAmount,
                    NewDiscountPercentage = saleItem.ItemDiscountPercentage,
                    NewLineTotal = saleItem.LineTotal,
                    NewCartTotal = sale?.TotalAmount ?? 0
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new DiscountOperationResult
                {
                    Success = false,
                    Message = $"Error applying discount: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Remove discount from specific item
        /// </summary>
        public async Task<DiscountOperationResult> RemoveItemDiscountAsync(int saleItemId)
        {
            return await ApplyItemDiscountAsync(saleItemId, 0);
        }

        /// <summary>
        /// Get discount summary for a sale
        /// </summary>
        public async Task<object> GetSaleDiscountSummaryAsync(int saleId)
        {
            var sale = await GetSaleByIdAsync(saleId);
            if (sale == null) return null;

            return new
            {
                SaleId = saleId,
                TotalItems = sale.SaleItems.Count,
                ItemsWithDiscounts = sale.ItemsWithDiscountCount,
                TotalItemDiscounts = sale.TotalItemDiscounts,
                EffectiveDiscountPercentage = sale.EffectiveDiscountPercentage,
                AverageDiscountPercentage = sale.AverageDiscountPercentage,
                ItemDiscounts = sale.SaleItems.Where(si => si.HasItemDiscount).Select(si => new
                {
                    ItemId = si.Id,
                    ProductName = si.ProductName,
                    DiscountPercentage = si.ItemDiscountPercentage,
                    DiscountAmount = si.ItemDiscountAmount,
                    LineTotal = si.LineTotal
                }).ToList()
            };
        }

        /// <summary>
        /// CRITICAL: Calculate cart totals with item-level discounts
        /// </summary>
        public CartTotals CalculateCartTotalsWithDiscounts(List<CartItemViewModel> cartItems)
        {
            var subtotal = cartItems.Sum(i => i.LineSubtotal);
            var discountAmount = cartItems.Sum(i => i.ItemDiscountAmount);
            var gstAmount = cartItems.Sum(i => i.LineGSTAmount);
            var total = cartItems.Sum(i => i.LineTotalWithDiscount);

            return new CartTotals
            {
                ItemCount = cartItems.Sum(i => i.Quantity),
                Subtotal = subtotal,
                DiscountAmount = discountAmount,
                GSTAmount = gstAmount,
                Total = total,
                ItemsWithDiscounts = cartItems.Count(i => i.HasDiscount),
                EffectiveDiscountPercentage = subtotal > 0 ? (discountAmount / subtotal) * 100 : 0
            };
        }
    }

    /// <summary>
    /// Cart totals with discount information
    /// </summary>
    public class CartTotals
    {
        public decimal ItemCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal Total { get; set; }
        public int ItemsWithDiscounts { get; set; }
        public decimal EffectiveDiscountPercentage { get; set; }
    }

    /// <summary>
    /// Discount operation result
    /// </summary>
    public class DiscountOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal NewDiscountAmount { get; set; }
        public decimal NewDiscountPercentage { get; set; }
        public decimal NewLineTotal { get; set; }
        public decimal NewCartTotal { get; set; }
    }
}
