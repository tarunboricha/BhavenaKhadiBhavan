using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    public class ReturnService : IReturnService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReturnService> _logger;

        public ReturnService(ApplicationDbContext context, ILogger<ReturnService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// CRITICAL FIX: Get returnable quantities for each sale item
        /// This properly handles partial returns
        /// </summary>
        public async Task<Dictionary<int, decimal>> GetReturnableQuantitiesAsync(int saleId)
        {
            var saleItems = await _context.SaleItems
                .Where(si => si.SaleId == saleId)
                .Select(si => new {
                    si.Id,
                    si.Quantity,
                    si.ReturnedQuantity
                })
                .ToListAsync();

            return saleItems.ToDictionary(
                si => si.Id,
                si => si.Quantity - si.ReturnedQuantity
            );
        }

        /// <summary>
        /// CRITICAL FIX: Get detailed returnable item information
        /// </summary>
        public async Task<List<ReturnableItemInfo>> GetReturnableItemsAsync(int saleId)
        {
            var saleItems = await _context.SaleItems
                .Include(si => si.Product)
                .Where(si => si.SaleId == saleId)
                .ToListAsync();

            var returnableItems = new List<ReturnableItemInfo>();

            foreach (var saleItem in saleItems)
            {
                var returnableQuantity = saleItem.Quantity - saleItem.ReturnedQuantity;

                if (returnableQuantity > 0)
                {
                    returnableItems.Add(new ReturnableItemInfo
                    {
                        SaleItemId = saleItem.Id,
                        ProductId = saleItem.ProductId,
                        ProductName = saleItem.ProductName,
                        OriginalQuantity = saleItem.Quantity,
                        AlreadyReturnedQuantity = saleItem.ReturnedQuantity,
                        ReturnableQuantity = returnableQuantity,
                        UnitPrice = saleItem.UnitPrice,
                        GSTRate = saleItem.GSTRate,
                        UnitOfMeasure = saleItem.UnitOfMeasure ?? "Piece"
                    });
                }
            }

            return returnableItems;
        }

        /// <summary>
        /// CRITICAL FIX: Calculate return totals with proper discount handling
        /// </summary>
        public async Task<ReturnCalculationResult> CalculateReturnTotalsAsync(int saleId, Dictionary<int, decimal> returnQuantities)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale == null)
                throw new ArgumentException("Sale not found");

            var result = new ReturnCalculationResult();

            foreach (var kvp in returnQuantities)
            {
                var saleItemId = kvp.Key;
                var returnQuantity = kvp.Value;

                if (returnQuantity <= 0) continue;

                var saleItem = sale.SaleItems.FirstOrDefault(si => si.Id == saleItemId);
                if (saleItem == null) continue;

                // Calculate proportional amounts
                var lineSubtotal = saleItem.UnitPrice * returnQuantity;
                var lineGST = lineSubtotal * (saleItem.GSTRate / 100);

                // CRITICAL: Apply proportional discount from original sale
                var totalBeforeDiscount = lineSubtotal + lineGST;
                var proportionalDiscount = totalBeforeDiscount * (sale.DiscountPercentage / 100);
                var lineTotal = totalBeforeDiscount - proportionalDiscount;

                result.SubTotal += lineSubtotal;
                result.GSTAmount += lineGST;
                result.DiscountAmount += proportionalDiscount;
                result.TotalAmount += lineTotal;

                result.Items.Add(new ReturnItemCalculation
                {
                    SaleItemId = saleItemId,
                    ProductName = saleItem.ProductName,
                    ReturnQuantity = returnQuantity,
                    UnitPrice = saleItem.UnitPrice,
                    LineSubtotal = lineSubtotal,
                    LineGST = lineGST,
                    LineDiscount = proportionalDiscount,
                    LineTotal = lineTotal,
                    UnitOfMeasure = saleItem.UnitOfMeasure ?? "Piece"
                });
            }

            return result;
        }

        /// <summary>
        /// CRITICAL FIX: Validate return quantities against available quantities
        /// </summary>
        public async Task<bool> ValidateReturnQuantitiesAsync(int saleId, Dictionary<int, decimal> returnQuantities)
        {
            var returnableQuantities = await GetReturnableQuantitiesAsync(saleId);

            foreach (var kvp in returnQuantities)
            {
                var saleItemId = kvp.Key;
                var requestedQuantity = kvp.Value;

                // Check if item exists and has returnable quantity
                if (!returnableQuantities.ContainsKey(saleItemId))
                {
                    _logger.LogWarning("Sale item {SaleItemId} not found or not returnable", saleItemId);
                    return false;
                }

                // Check if requested quantity doesn't exceed available
                if (requestedQuantity > returnableQuantities[saleItemId])
                {
                    _logger.LogWarning("Requested return quantity {RequestedQuantity} exceeds available {AvailableQuantity} for sale item {SaleItemId}",
                        requestedQuantity, returnableQuantities[saleItemId], saleItemId);
                    return false;
                }

                // Check for positive quantity
                if (requestedQuantity <= 0)
                {
                    _logger.LogWarning("Invalid return quantity {RequestedQuantity} for sale item {SaleItemId}",
                        requestedQuantity, saleItemId);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ENHANCED: Create return with proper quantity handling
        /// </summary>
        public async Task<Return> CreateReturnAsync(Return returnEntity, List<ReturnItem> returnItems)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Creating return for sale {SaleId} with {ItemCount} items",
                    returnEntity.SaleId, returnItems.Count);

                // Generate return number
                returnEntity.ReturnNumber = await GetNextReturnNumberAsync();
                returnEntity.ReturnDate = DateTime.Now;

                // Validate all return quantities
                var returnQuantities = returnItems.ToDictionary(ri => ri.SaleItemId, ri => ri.ReturnQuantity);
                if (!await ValidateReturnQuantitiesAsync(returnEntity.SaleId, returnQuantities))
                {
                    throw new InvalidOperationException("Invalid return quantities");
                }

                // Calculate accurate totals
                var calculation = await CalculateReturnTotalsAsync(returnEntity.SaleId, returnQuantities);

                returnEntity.SubTotal = calculation.SubTotal;
                returnEntity.GSTAmount = calculation.GSTAmount;
                returnEntity.DiscountAmount = calculation.DiscountAmount;
                returnEntity.TotalAmount = calculation.TotalAmount;

                // Add return to database
                _context.Returns.Add(returnEntity);
                await _context.SaveChangesAsync();

                // Process each return item
                foreach (var returnItem in returnItems)
                {
                    returnItem.ReturnId = returnEntity.Id;

                    // Get matching calculation
                    var calc = calculation.Items.FirstOrDefault(i => i.SaleItemId == returnItem.SaleItemId);
                    if (calc != null)
                    {
                        returnItem.GSTAmount = calc.LineGST;
                        returnItem.DiscountAmount = calc.LineDiscount;
                        returnItem.LineTotal = calc.LineTotal;
                    }

                    // Update sale item returned quantity
                    var saleItem = await _context.SaleItems.FindAsync(returnItem.SaleItemId);
                    if (saleItem != null)
                    {
                        saleItem.ReturnedQuantity += returnItem.ReturnQuantity;

                        _logger.LogInformation("Updated sale item {SaleItemId}: returned quantity {ReturnedQuantity} of {TotalQuantity}",
                            saleItem.Id, saleItem.ReturnedQuantity, saleItem.Quantity);
                    }

                    // Restore stock to product
                    var product = await _context.Products.FindAsync(returnItem.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += returnItem.ReturnQuantity;
                        product.UpdatedAt = DateTime.Now;

                        _logger.LogInformation("Restored {Quantity} {Unit} to product {ProductName} stock",
                            returnItem.ReturnQuantity, returnItem.UnitOfMeasure, product.Name);
                    }

                    _context.ReturnItems.Add(returnItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Return {ReturnNumber} created successfully with total refund ₹{TotalAmount}",
                    returnEntity.ReturnNumber, returnEntity.TotalAmount);

                return returnEntity;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating return for sale {SaleId}", returnEntity.SaleId);
                throw;
            }
        }

        /// <summary>
        /// Get return by ID with all related data
        /// </summary>
        public async Task<Return?> GetReturnByIdAsync(int id)
        {
            return await _context.Returns
                .Include(r => r.Sale)
                    .ThenInclude(s => s.Customer)
                .Include(r => r.ReturnItems)
                    .ThenInclude(ri => ri.Product)
                .Include(r => r.ReturnItems)
                    .ThenInclude(ri => ri.SaleItem)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        /// <summary>
        /// Get returns with filtering
        /// </summary>
        public async Task<List<Return>> GetReturnsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Returns
                .Include(r => r.Sale)
                .Include(r => r.ReturnItems)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.ReturnDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.ReturnDate.Date <= toDate.Value.Date);
            }

            return await query
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();
        }

        /// <summary>
        /// Process return (mark as completed)
        /// </summary>
        public async Task<bool> ProcessReturnAsync(int returnId)
        {
            try
            {
                var returnEntity = await _context.Returns.FindAsync(returnId);
                if (returnEntity == null) return false;

                returnEntity.Status = "Completed";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Return {ReturnId} processed successfully", returnId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing return {ReturnId}", returnId);
                return false;
            }
        }

        /// <summary>
        /// Generate next return number
        /// </summary>
        public async Task<string> GetNextReturnNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"RET{today:yyyyMMdd}";

            var lastReturn = await _context.Returns
                .Where(r => r.ReturnNumber.StartsWith(prefix))
                .OrderByDescending(r => r.ReturnNumber)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastReturn != null && lastReturn.ReturnNumber.Length > prefix.Length)
            {
                var lastSequence = lastReturn.ReturnNumber.Substring(prefix.Length);
                if (int.TryParse(lastSequence, out int lastNum))
                {
                    sequence = lastNum + 1;
                }
            }

            return $"{prefix}{sequence:D3}";
        }
    }
}
