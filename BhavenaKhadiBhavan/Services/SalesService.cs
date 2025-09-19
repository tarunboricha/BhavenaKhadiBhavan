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

                // Add sale items and update stock
                foreach (var item in saleItems)
                {
                    item.SaleId = sale.Id;

                    // Get product for stock update
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found");
                    }

                    // CRITICAL: Update stock with decimal quantity
                    if (product.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient stock for {product.Name}. Available: {product.StockQuantity}, Required: {item.Quantity}");
                    }

                    // Update stock with decimal precision
                    product.StockQuantity -= item.Quantity;
                    product.UpdatedAt = DateTime.Now;

                    // Set unit of measure
                    item.UnitOfMeasure = product.UnitOfMeasure ?? "Piece";

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
                throw;
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

        public async Task<List<Sale>> GetSalesAsync(DateTime? fromDate = null, DateTime? toDate = null)
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

        public async Task<string> GenerateInvoiceNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"INV{today:yyyyMMdd}";

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

            return $"{prefix}{sequence:D3}";
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
