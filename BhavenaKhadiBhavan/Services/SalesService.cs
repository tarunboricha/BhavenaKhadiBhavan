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

        public async Task<Sale> CreateSaleAsync(Sale sale, List<SaleItem> cartItems)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Generate invoice number
                sale.InvoiceNumber = await GenerateInvoiceNumberAsync();
                sale.SaleDate = DateTime.Now;

                // Calculate totals with proper decimal handling
                decimal subtotal = 0;
                decimal totalGST = 0;

                foreach (var item in cartItems)
                {
                    // CRITICAL: Proper decimal calculation
                    var itemSubtotal = item.UnitPrice * item.Quantity;
                    var itemGST = itemSubtotal * item.GSTRate / 100;
                    var itemTotal = itemSubtotal + itemGST;

                    item.GSTAmount = itemGST;
                    item.LineTotal = itemTotal;

                    subtotal += itemSubtotal;
                    totalGST += itemGST;
                }

                // Set sale totals
                sale.SubTotal = subtotal;
                sale.GSTAmount = totalGST;

                // Apply discount if any
                var discountAmount = (subtotal + totalGST) * sale.DiscountPercentage / 100;
                sale.DiscountAmount = discountAmount;
                sale.TotalAmount = subtotal + totalGST - discountAmount;

                // Add sale to context
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Add sale items and update stock
                foreach (var item in cartItems)
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
                return itemSubtotal * (item.GSTRate / 100);
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
    }
}
