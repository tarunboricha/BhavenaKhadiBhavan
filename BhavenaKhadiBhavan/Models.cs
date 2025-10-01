using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BhavenaKhadiBhavan.Models
{
    /// <summary>
    /// Simple Product model for Khadi stores
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? SKU { get; set; }

        [StringLength(100)]
        public string? FabricType { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(20)]
        public string? Size { get; set; }

        [StringLength(50)]
        public string? Pattern { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal GSTRate { get; set; } = 5;

        // CRITICAL: Change from int to decimal for fractional quantities
        [Required]
        [Range(0, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]  // Support up to 3 decimal places
        public decimal StockQuantity { get; set; }

        [Range(0, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]
        public decimal MinimumStock { get; set; } = 5;

        // Unit of measurement for the product
        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece"; // Piece, Meter, Kg, etc.

        public bool IsActive { get; set; } = true;

        // Foreign Keys
        [Required]
        public int CategoryId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual Category? Category { get; set; }

        // Computed Properties
        public string DisplayName => $"{Name}" +
            (!string.IsNullOrEmpty(Color) ? $" - {Color}" : "") +
            (!string.IsNullOrEmpty(Size) ? $" ({Size})" : "");

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceWithGST => SalePrice + (SalePrice * GSTRate / 100);

        public bool IsLowStock => StockQuantity <= MinimumStock;

        public string StockStatus => StockQuantity == 0 ? "Out of Stock" :
                                   IsLowStock ? "Low Stock" : "In Stock";

        [Column(TypeName = "decimal(5,2)")]
        public decimal ProfitMargin => SalePrice > 0 ? ((SalePrice - PurchasePrice) / SalePrice) * 100 : 0;

        // Display stock with proper formatting
        public string DisplayStock => $"{StockQuantity:0.###} {UnitOfMeasure}";

        // Add this new field to the Product class (around line 25)
        [StringLength(50)]
        [Display(Name = "Barcode/EAN")]
        public string? Barcode { get; set; }  // Optional traditional barcode (EAN/UPC)

        // Add this computed property to the Product class (around line 95, with other computed properties)
        /// <summary>
        /// Gets the primary scannable code (SKU takes priority, then Barcode)
        /// </summary>
        [NotMapped]
        public string PrimaryBarcodeValue => !string.IsNullOrEmpty(SKU) ? SKU : (Barcode ?? string.Empty);

        /// <summary>
        /// Check if product has any scannable code
        /// </summary>
        [NotMapped]
        public bool HasScannableCode => !string.IsNullOrEmpty(SKU) || !string.IsNullOrEmpty(Barcode);

        /// <summary>
        /// Display text for scanning
        /// </summary>
        [NotMapped]
        public string ScannableCodeDisplay => !string.IsNullOrEmpty(SKU) ? $"SKU: {SKU}" :
                                             (!string.IsNullOrEmpty(Barcode) ? $"Barcode: {Barcode}" : "No Code");

        // INSTRUCTION: Add these fields to your existing Product class in Models.cs
        // Do not replace the entire class, just add these new properties
    }

    /// <summary>
    /// Simple Category model
    /// </summary>
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    /// <summary>
    /// Simple Customer model
    /// </summary>
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Phone]
        [StringLength(15)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Calculated Fields
        public int TotalOrders { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPurchases { get; set; }

        public DateTime? LastPurchaseDate { get; set; }

        // Computed Properties
        public string CustomerType
        {
            get
            {
                return TotalOrders switch
                {
                    0 => "New",
                    1 => "Second-time",
                    >= 2 and < 5 => "Regular",
                    >= 5 => "Loyal",
                    _ => "Unknown"
                };
            }
        }

        public int DaysSinceLastPurchase => LastPurchaseDate.HasValue ?
            (int)(DateTime.Now - LastPurchaseDate.Value).TotalDays : 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageOrderValue => TotalOrders > 0 ? TotalPurchases / TotalOrders : 0;
    }

    /// <summary>
    /// Enhanced SaleItem model with individual item-level discounts
    /// </summary>
    public partial class SaleItem
    {
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Range(0.001, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal GSTRate { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal ReturnedQuantity { get; set; } = 0;

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece";

        // CRITICAL: Enhanced item-level discount fields
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ItemDiscountPercentage { get; set; } = 0;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ItemDiscountAmount { get; set; } = 0;

        // Navigation Properties
        public virtual Sale? Sale { get; set; }
        public virtual Product? Product { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Enhanced Computed Properties for Item-Level Discounts
        [NotMapped]
        public decimal LineSubtotal => UnitPrice * Quantity;

        [NotMapped]
        public decimal LineSubtotalAfterDiscount => LineSubtotal - ItemDiscountAmount;

        [NotMapped]
        public decimal LineGSTAmount => LineSubtotalAfterDiscount * GSTRate / 100;

        [NotMapped]
        public decimal LineTotalWithDiscount => LineSubtotalAfterDiscount + LineGSTAmount;

        [NotMapped]
        public bool HasItemDiscount => ItemDiscountPercentage > 0 || ItemDiscountAmount > 0;

        [NotMapped]
        public string DiscountDisplay => HasItemDiscount ?
            $"{ItemDiscountPercentage:0.##}% (-₹{ItemDiscountAmount:N2})" : "No Discount";

        [NotMapped]
        public decimal ReturnableQuantity => Quantity - ReturnedQuantity;

        [NotMapped]
        public bool CanBeReturned => ReturnableQuantity > 0;

        [NotMapped]
        public string DisplayQuantity => $"{Quantity:0.###} {UnitOfMeasure}";

        // CRITICAL: Methods for discount calculations
        public void ApplyDiscountPercentage(decimal discountPercentage)
        {
            ItemDiscountPercentage = discountPercentage;
            ItemDiscountAmount = LineSubtotal * discountPercentage / 100;
        }

        public void ApplyDiscountAmount(decimal discountAmount)
        {
            ItemDiscountAmount = discountAmount;
            ItemDiscountPercentage = LineSubtotal > 0 ? (discountAmount / LineSubtotal) * 100 : 0;
        }

        public void ClearDiscount()
        {
            ItemDiscountPercentage = 0;
            ItemDiscountAmount = 0;
        }
    }

    /// <summary>
    /// Enhanced Sale model with computed discount properties (hiding overall discount percentage)
    /// </summary>
    public partial class Sale
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Sale Date")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters")]
        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }

        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
        [Display(Name = "Customer Phone")]
        public string? CustomerPhone { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "Payment Reference")]
        public string? PaymentReference { get; set; }

        [Display(Name = "Subtotal (₹)")]
        public decimal SubTotal { get; set; }

        [Display(Name = "GST Amount (₹)")]
        public decimal GSTAmount { get; set; }

        // CRITICAL: Keep for backward compatibility but hide in UI
        [Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100")]
        [Display(Name = "Overall Discount (%)")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Display(Name = "Discount Amount (₹)")]
        public decimal DiscountAmount { get; set; }

        [Display(Name = "Total Amount (₹)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed";

        [StringLength(300)]
        public string? Notes { get; set; }

        // Navigation Properties
        public virtual Customer? Customer { get; set; }
        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

        // ENHANCED: Computed Properties for Item-Level Discounts
        [NotMapped]
        public string CustomerDisplayName => CustomerName ?? Customer?.Name ?? "Walk-in Customer";

        [NotMapped]
        public decimal ItemCount => SaleItems?.Sum(si => si.Quantity) ?? 0;

        [NotMapped]
        public bool HasCustomer => CustomerId.HasValue || !string.IsNullOrEmpty(CustomerName);

        [NotMapped]
        public decimal ReturnedAmount => Returns?.Where(r => r.Status == "Completed").Sum(r => r.TotalAmount) ?? 0;

        [NotMapped]
        public decimal NetAmount => TotalAmount - ReturnedAmount;

        // CRITICAL: New item-level discount computed properties
        [NotMapped]
        public decimal TotalItemDiscounts => SaleItems?.Sum(i => i.ItemDiscountAmount) ?? 0;

        [NotMapped]
        public bool HasItemLevelDiscounts => SaleItems?.Any(i => i.HasItemDiscount) ?? false;

        [NotMapped]
        public decimal EffectiveDiscountPercentage =>
            SubTotal > 0 ? (TotalItemDiscounts / SubTotal) * 100 : 0;

        [NotMapped]
        public int ItemsWithDiscountCount => SaleItems?.Count(i => i.HasItemDiscount) ?? 0;

        [NotMapped]
        public decimal AverageDiscountPercentage =>
            ItemsWithDiscountCount > 0 ?
            SaleItems?.Where(i => i.HasItemDiscount).Average(i => i.ItemDiscountPercentage) ?? 0 : 0;

        // CRITICAL: Methods for applying overall discounts to individual items
        public void ApplyOverallDiscountToItems(decimal overallDiscountPercentage)
        {
            if (SaleItems == null) return;

            foreach (var item in SaleItems)
            {
                item.ApplyDiscountPercentage(overallDiscountPercentage);
            }

            // Update overall discount fields for compatibility
            DiscountPercentage = overallDiscountPercentage;
            RecalculateTotals();
        }

        public void RecalculateTotals()
        {
            if (SaleItems == null) return;

            SubTotal = SaleItems.Sum(i => i.LineSubtotal);
            DiscountAmount = SaleItems.Sum(i => i.ItemDiscountAmount);
            GSTAmount = SaleItems.Sum(i => i.LineGSTAmount);
            TotalAmount = SaleItems.Sum(i => i.LineTotalWithDiscount);
        }

        public void ClearAllItemDiscounts()
        {
            if (SaleItems == null) return;

            foreach (var item in SaleItems)
            {
                item.ClearDiscount();
            }

            DiscountPercentage = 0;
            RecalculateTotals();
        }

        /// <summary>
        /// Original calculated total before any payment adjustments
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Calculated Total (₹)")]
        public decimal CalculatedTotal { get; set; }

        /// <summary>
        /// Actual amount received from customer
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount Received (₹)")]
        public decimal AmountReceived { get; set; }

        /// <summary>
        /// Payment adjustment (difference between calculated and received)
        /// Positive = Customer paid extra, Negative = Customer paid less
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Payment Adjustment (₹)")]
        public decimal PaymentAdjustment { get; set; }

        /// <summary>
        /// Reason for payment adjustment
        /// </summary>
        [StringLength(200)]
        [Display(Name = "Adjustment Reason")]
        public string? AdjustmentReason { get; set; }

        /// <summary>
        /// Type of adjustment: "Customer_Convenience", "Cash_Shortage", "System_Error", etc.
        /// </summary>
        [StringLength(50)]
        [Display(Name = "Adjustment Type")]
        public string? AdjustmentType { get; set; }

        /// <summary>
        /// Staff member who processed this payment
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Processed By")]
        public string? ProcessedBy { get; set; }

        /// <summary>
        /// Whether this sale requires manager approval for adjustment
        /// </summary>
        public bool RequiresApproval { get; set; } = false;

        /// <summary>
        /// Manager who approved the adjustment
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        /// <summary>
        /// When the adjustment was approved
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        // **COMPUTED PROPERTIES FOR PAYMENT ANALYSIS**

        /// <summary>
        /// Check if payment has any adjustment
        /// </summary>
        [NotMapped]
        public bool HasPaymentAdjustment => Math.Abs(PaymentAdjustment) > 0.01m;

        /// <summary>
        /// Check if customer paid less than calculated amount
        /// </summary>
        [NotMapped]
        public bool IsShortPayment => PaymentAdjustment < -0.01m;

        /// <summary>
        /// Check if customer paid more than calculated amount
        /// </summary>
        [NotMapped]
        public bool IsOverPayment => PaymentAdjustment > 0.01m;

        /// <summary>
        /// Percentage of adjustment relative to calculated total
        /// </summary>
        [NotMapped]
        public decimal AdjustmentPercentage =>
            CalculatedTotal > 0 ? Math.Abs(PaymentAdjustment) / CalculatedTotal * 100 : 0;

        /// <summary>
        /// Display-friendly adjustment description
        /// </summary>
        [NotMapped]
        public string AdjustmentDisplay
        {
            get
            {
                if (!HasPaymentAdjustment) return "Exact Payment";
                if (IsShortPayment) return $"Short by ₹{Math.Abs(PaymentAdjustment):N2}";
                if (IsOverPayment) return $"Over by ₹{PaymentAdjustment:N2}";
                return "No Adjustment";
            }
        }

        /// <summary>
        /// Payment status for reporting
        /// </summary>
        [NotMapped]
        public string PaymentStatus
        {
            get
            {
                if (!HasPaymentAdjustment) return "Exact";
                if (Math.Abs(PaymentAdjustment) <= 5) return "Minor Adjustment";
                if (Math.Abs(PaymentAdjustment) <= 20) return "Moderate Adjustment";
                return "Significant Adjustment";
            }
        }

        /// <summary>
        /// Whether adjustment needs approval based on amount
        /// </summary>
        [NotMapped]
        public bool ShouldRequireApproval
        {
            get
            {
                // Require approval for adjustments > ₹20 or > 2% of total
                return Math.Abs(PaymentAdjustment) > 20 || AdjustmentPercentage > 2;
            }
        }
    }

    /// <summary>
    /// Enhanced view model for cart items with discount support
    /// </summary>
    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTRate { get; set; }
        public string? UnitOfMeasure { get; set; } = "Piece";

        // Item-level discount fields
        public decimal ItemDiscountPercentage { get; set; } = 0;
        public decimal ItemDiscountAmount { get; set; } = 0;

        // Computed properties
        public decimal LineSubtotal => UnitPrice * Quantity;
        public decimal LineSubtotalAfterDiscount => LineSubtotal - ItemDiscountAmount;
        public decimal LineGSTAmount => LineSubtotalAfterDiscount * GSTRate / 100;
        public decimal LineTotalWithDiscount => LineSubtotalAfterDiscount + LineGSTAmount;
        public bool HasDiscount => ItemDiscountPercentage > 0 || ItemDiscountAmount > 0;

        // Methods for discount manipulation
        public void ApplyDiscountPercentage(decimal discountPercentage)
        {
            ItemDiscountPercentage = discountPercentage;
            ItemDiscountAmount = LineSubtotal * discountPercentage / 100;
        }

        public void ApplyDiscountAmount(decimal discountAmount)
        {
            ItemDiscountAmount = discountAmount;
            ItemDiscountPercentage = LineSubtotal > 0 ? (discountAmount / LineSubtotal) * 100 : 0;
        }
    }

    /// <summary>
    /// Enhanced sales view model with item-level discount support
    /// </summary>
    public class SalesViewModel
    {
        public Sale Sale { get; set; } = new Sale();
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Customer> Customers { get; set; } = new List<Customer>();
        public List<CartItemViewModel> CartItems { get; set; } = new List<CartItemViewModel>();

        // ENHANCED: Cart totals with item-level discounts
        [Column(TypeName = "decimal(18,2)")]
        public decimal CartSubtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartDiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartGST { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartTotal { get; set; }

        // Computed properties for discounts
        public bool HasItemDiscounts => CartItems.Any(i => i.HasDiscount);
        public int ItemsWithDiscountCount => CartItems.Count(i => i.HasDiscount);
        public decimal EffectiveDiscountPercentage =>
            CartSubtotal > 0 ? (CartDiscountAmount / CartSubtotal) * 100 : 0;

        // Payment Processing Fields
        [Display(Name = "Amount Received from Customer (₹)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountReceived { get; set; }

        [Display(Name = "Reason for Payment Adjustment")]
        public string? PaymentAdjustmentReason { get; set; }

        // Computed Properties
        public decimal PaymentAdjustment => AmountReceived - CartTotal;
        public bool HasPaymentAdjustment => Math.Abs(PaymentAdjustment) > 0.01m;
        public bool RequiresManagerApproval => Math.Abs(PaymentAdjustment) > 20 ||
            (CartTotal > 0 && Math.Abs(PaymentAdjustment) / CartTotal * 100 > 2);

        public string PaymentAdjustmentDisplay
        {
            get
            {
                if (!HasPaymentAdjustment) return "Exact payment";
                if (PaymentAdjustment < 0) return $"Short by ₹{Math.Abs(PaymentAdjustment):N2}";
                return $"Over by ₹{PaymentAdjustment:N2}";
            }
        }
    }


    /// <summary>
    /// Result of payment processing operation
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal PaymentAdjustment { get; set; }
        public bool RequiresApproval { get; set; }
        public string? AdjustmentType { get; set; }
    }

    /// <summary>
    /// Comprehensive payment reconciliation report
    /// </summary>
    public class PaymentReconciliationReport
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Overall Statistics
        public int TotalSales { get; set; }
        public decimal TotalCalculatedAmount { get; set; }
        public decimal TotalReceivedAmount { get; set; }
        public decimal TotalAdjustment { get; set; }

        // Adjustment Statistics
        public int SalesWithAdjustments { get; set; }
        public int ShortPayments { get; set; }
        public int OverPayments { get; set; }
        public decimal TotalShortAmount { get; set; }
        public decimal TotalOverAmount { get; set; }
        public int PendingApprovals { get; set; }

        // Computed Properties
        public decimal AdjustmentPercentage => TotalCalculatedAmount > 0 ?
            Math.Abs(TotalAdjustment) / TotalCalculatedAmount * 100 : 0;

        public decimal NetCashVariance => TotalAdjustment; // Positive = more cash, Negative = less cash

        public string CashVarianceStatus =>
            Math.Abs(NetCashVariance) <= 10 ? "Balanced" :
            NetCashVariance > 10 ? "Cash Surplus" : "Cash Shortage";

        // Detailed Breakdowns
        public List<AdjustmentTypeSummary> AdjustmentsByType { get; set; } = new();
        public List<DailyPaymentSummary> DailyBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Summary of adjustments by type
    /// </summary>
    public class AdjustmentTypeSummary
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }

        public string TypeDescription => Type switch
        {
            "Customer_Convenience" => "Customer didn't have exact change",
            "Cash_Shortage" => "Customer short on cash",
            "System_Error" => "Potential system or pricing error",
            "Manager_Discretion" => "Manager approved adjustment",
            _ => "Other adjustment"
        };
    }

    /// <summary>
    /// Daily payment summary for reconciliation
    /// </summary>
    public class DailyPaymentSummary
    {
        public DateTime Date { get; set; }
        public int SalesCount { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal AdjustmentAmount { get; set; }
        public int SalesWithAdjustments { get; set; }

        public decimal AdjustmentPercentage => CalculatedAmount > 0 ?
            Math.Abs(AdjustmentAmount) / CalculatedAmount * 100 : 0;

        public string DayStatus =>
            Math.Abs(AdjustmentAmount) <= 20 ? "Good" :
            Math.Abs(AdjustmentAmount) <= 50 ? "Fair" : "Needs Review";
    }

    /// <summary>
    /// Adjustment reason analysis
    /// </summary>
    public class AdjustmentReasonSummary
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public decimal Percentage { get; set; } // Percentage of total adjustments
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

    /// <summary>
    /// Simple Return model
    /// </summary>
    public class Return
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ReturnNumber { get; set; } = string.Empty;

        [Required]
        public int SaleId { get; set; }

        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed";

        // Navigation Properties
        public virtual Sale? Sale { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Computed Properties
        public string CustomerName => Sale?.CustomerDisplayName ?? "Unknown";
    }

    /// <summary>
    /// Return Item model
    /// </summary>
    public class ReturnItem
    {
        public int Id { get; set; }

        [Required]
        public int ReturnId { get; set; }

        [Required]
        public int SaleItemId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        // CRITICAL: Change from int to decimal
        [Required]
        [Range(0.001, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]
        public decimal ReturnQuantity { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal GSTRate { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece";

        // Navigation Properties
        public virtual Return? Return { get; set; }
        public virtual SaleItem? SaleItem { get; set; }
        public virtual Product? Product { get; set; }

        public string DisplayQuantity => $"{ReturnQuantity:0.###} {UnitOfMeasure}";
    }

    /// <summary>
    /// Simple User model for basic authentication
    /// </summary>
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string Role { get; set; } = "Staff"; // Admin, Staff

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Last Login")]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Computed Properties
        public bool IsAdmin => Role == "Admin";
        public string RoleDisplayName => Role;
        public string StatusDisplayName => IsActive ? "Active" : "Inactive";
    }

    /// <summary>
    /// Simple settings model for store configuration
    /// </summary>
    public class Setting
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Value { get; set; }

        [StringLength(100)]
        public string Category { get; set; } = "General";

        [StringLength(200)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// View Models for UI
    /// </summary>
    public class DashboardViewModel
    {
        [Column(TypeName = "decimal(18,2)")]
        public decimal TodaySales { get; set; }

        public int TodayOrders { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthSales { get; set; }

        public int TotalCustomers { get; set; }
        public int LowStockCount { get; set; }

        public List<Product> LowStockProducts { get; set; } = new List<Product>();
        public List<Sale> RecentSales { get; set; } = new List<Sale>();
        public List<Customer> RecentCustomers { get; set; } = new List<Customer>();
    }

    public class ReportsViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime ToDate { get; set; } = DateTime.Today;

        // Sales Report
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<Sale> Sales { get; set; } = new();

        // Stock Report
        public List<Product> Products { get; set; } = new();
        public List<Product> LowStockProducts { get; set; } = new();
        public decimal TotalStockValue { get; set; }

        // Customer Report  
        public List<Customer> TopCustomers { get; set; } = new();
        public int NewCustomers { get; set; }

        // GST Report
        public decimal TotalGST { get; set; }
        public Dictionary<decimal, decimal> GSTBreakdown { get; set; } = new();
    }
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }

    /// <summary>
    /// Enhanced Sales Report with Profit Margins and Item-Level Discount Analysis
    /// </summary>
    public class SalesReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<Sale> Sales { get; set; } = new();

        // Sales Summary
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalItemsSold { get; set; }

        // Financial Breakdown
        public decimal TotalSubtotal { get; set; }
        public decimal TotalItemDiscounts { get; set; }
        public decimal TotalGST { get; set; }
        public decimal TotalCostOfGoodsSold { get; set; }
        public decimal TotalGrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }

        // Discount Analysis
        public decimal EffectiveDiscountPercentage { get; set; }
        public int SalesWithDiscounts { get; set; }
        public decimal AverageDiscountPerSale { get; set; }
        public decimal DiscountPenetration { get; set; } // % of sales with discounts

        // Payment Method Breakdown
        public List<PaymentMethodSummary> PaymentMethodBreakdown { get; set; } = new();

        // Daily Breakdown
        public List<DailySalesSummary> DailyBreakdown { get; set; } = new();

        // Top Products
        public List<TopProductSummary> TopProducts { get; set; } = new();
    }

    /// <summary>
    /// Enhanced Daily Sales Report with Hour-by-Hour Analysis
    /// </summary>
    public class DailySalesReportViewModel
    {
        public DateTime Date { get; set; }
        public List<Sale> Sales { get; set; } = new();

        // Daily Summary
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalItemsSold { get; set; }
        public decimal TotalGrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }

        // Hourly Breakdown
        public List<HourlySalesSummary> HourlyBreakdown { get; set; } = new();

        // Payment Methods
        public List<PaymentMethodSummary> PaymentMethodBreakdown { get; set; } = new();

        // Top Products of the Day
        public List<TopProductSummary> TopProducts { get; set; } = new();

        // Customer Analysis
        public int UniqueCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
    }

    /// <summary>
    /// Comprehensive Stock Report with Movement Analysis
    /// </summary>
    public class StockReportViewModel
    {
        public List<Product> Products { get; set; } = new();

        // Stock Summary
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int LowStockProducts { get; set; }
        public int OutOfStockProducts { get; set; }
        public decimal TotalStockValue { get; set; }
        public decimal TotalSaleValue { get; set; }
        public decimal PotentialProfit { get; set; }

        // Category Wise Stock
        public List<CategoryStockSummary> CategoryWiseStock { get; set; } = new();

        // Fast/Slow Moving Analysis
        public List<Product> FastMovingProducts { get; set; } = new();
        public List<Product> SlowMovingProducts { get; set; } = new();

        // Stock Age Analysis
        public decimal AverageStockAge { get; set; }
        public List<StockAgingSummary> StockAging { get; set; } = new();
    }

    /// <summary>
    /// Stock Movement Analysis Report
    /// </summary>
    public class StockMovementReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Movement Summary
        public List<ProductMovementSummary> ProductMovements { get; set; } = new();

        // Top Movers
        public List<TopProductSummary> TopSellingProducts { get; set; } = new();
        public List<Product> LeastSellingProducts { get; set; } = new();

        // Stock Turnover Analysis
        public List<StockTurnoverSummary> StockTurnover { get; set; } = new();
    }

    /// <summary>
    /// Profit Margin Analysis Report
    /// </summary>
    public class ProfitMarginReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Overall Profit Analysis
        public decimal TotalRevenue { get; set; }
        public decimal TotalCostOfGoodsSold { get; set; }
        public decimal TotalGrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }

        // Impact of Discounts on Profit
        public decimal ProfitWithoutDiscounts { get; set; }
        public decimal ProfitWithDiscounts { get; set; }
        public decimal DiscountImpactOnProfit { get; set; }

        // Category-wise Profit
        public List<CategoryProfitSummary> CategoryProfits { get; set; } = new();

        // Product-wise Profit Analysis
        public List<ProductProfitSummary> ProductProfits { get; set; } = new();

        // Profit Trends
        public List<DailyProfitSummary> DailyProfitTrends { get; set; } = new();
    }

    /// <summary>
    /// Product Profitability Analysis
    /// </summary>
    public class ProductProfitabilityReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Most Profitable Products
        public List<ProductProfitSummary> MostProfitableProducts { get; set; } = new();

        // Least Profitable Products
        public List<ProductProfitSummary> LeastProfitableProducts { get; set; } = new();

        // Products by Profit Margin Categories
        public List<ProductProfitSummary> HighMarginProducts { get; set; } = new(); // >50%
        public List<ProductProfitSummary> MediumMarginProducts { get; set; } = new(); // 20-50%
        public List<ProductProfitSummary> LowMarginProducts { get; set; } = new(); // <20%
        public List<ProductProfitSummary> LossProducts { get; set; } = new(); // <0%
    }

    /// <summary>
    /// Enhanced GST Report with Item-Level Discount Impact
    /// </summary>
    public class GSTReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // GST Summary
        public decimal TotalTaxableAmount { get; set; }
        public decimal TotalGSTAmount { get; set; }
        public decimal EffectiveGSTRate { get; set; }

        // GST Rate-wise Breakdown
        public List<GSTRateSummary> GSTRateBreakdown { get; set; } = new();

        // Impact of Discounts on GST
        public decimal GSTWithoutDiscounts { get; set; }
        public decimal GSTWithDiscounts { get; set; }
        public decimal DiscountImpactOnGST { get; set; }

        // Monthly GST Trends
        public List<MonthlyGSTSummary> MonthlyGSTTrends { get; set; } = new();
    }

    /// <summary>
    /// Customer Analysis Report
    /// </summary>
    public class CustomerReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Customer Summary
        public int TotalCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }

        // Customer Segmentation
        public List<Customer> TopCustomers { get; set; } = new();
        public List<CustomerSegmentSummary> CustomerSegments { get; set; } = new();

        // Customer Acquisition Trends
        public List<MonthlyCustomerSummary> CustomerAcquisitionTrends { get; set; } = new();
    }

    /// <summary>
    /// Item-Level Discount Analysis Report
    /// </summary>
    public class DiscountAnalysisReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Discount Summary
        public decimal TotalDiscountsGiven { get; set; }
        public decimal AverageDiscountPercentage { get; set; }
        public int SalesWithDiscounts { get; set; }
        public decimal DiscountPenetration { get; set; }

        // Item-Level Discount Analysis
        public List<ProductDiscountSummary> ProductDiscountAnalysis { get; set; } = new();

        // Discount Impact Analysis
        public decimal RevenueImpact { get; set; }
        public decimal ProfitImpact { get; set; }
        public decimal GSTImpact { get; set; }

        // Discount Trends
        public List<DailyDiscountSummary> DailyDiscountTrends { get; set; } = new();

        // Top Discounted Products
        public List<ProductDiscountSummary> MostDiscountedProducts { get; set; } = new();
    }

    /// <summary>
    /// Category Performance Analysis
    /// </summary>
    public class CategoryPerformanceReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Category Performance Summary
        public List<CategoryPerformanceSummary> CategoryPerformance { get; set; } = new();

        // Category Trends
        public List<CategoryTrendSummary> CategoryTrends { get; set; } = new();

        // Best and Worst Performing Categories
        public List<CategoryPerformanceSummary> BestPerformingCategories { get; set; } = new();
        public List<CategoryPerformanceSummary> WorstPerformingCategories { get; set; } = new();
    }

    /// <summary>
    /// Return Analysis Report
    /// </summary>
    public class ReturnAnalysisReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Return Summary
        public decimal TotalReturnAmount { get; set; }
        public int TotalReturns { get; set; }
        public decimal ReturnRate { get; set; } // % of sales returned
        public decimal AverageReturnValue { get; set; }

        // Return Reasons Analysis
        public List<ReturnReasonSummary> ReturnReasons { get; set; } = new();

        // Product Return Analysis
        public List<ProductReturnSummary> ProductReturns { get; set; } = new();

        // Return Trends
        public List<DailyReturnSummary> DailyReturnTrends { get; set; } = new();

        // Impact on Profit
        public decimal ProfitImpactOfReturns { get; set; }
    }

    // =============================
    // SUPPORTING SUMMARY CLASSES
    // =============================

    public class PaymentMethodSummary
    {
        public string Method { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class DailySalesSummary
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal DiscountAmount { get; set; }
    }

    public class HourlySalesSummary
    {
        public int Hour { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal GrossProfit { get; set; }
    }

    public class TopProductSummary
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }
        public decimal AverageDiscount { get; set; }
    }

    public class CategoryStockSummary
    {
        public string Category { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal TotalStock { get; set; }
        public decimal StockValue { get; set; }
        public decimal SaleValue { get; set; }
        public decimal PotentialProfit { get; set; }
    }

    public class StockAgingSummary
    {
        public string AgeRange { get; set; } = string.Empty; // 0-30 days, 31-60 days, etc.
        public int ProductCount { get; set; }
        public decimal StockValue { get; set; }
    }

    public class ProductMovementSummary
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal OpeningStock { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal ClosingStock { get; set; }
        public decimal StockTurnover { get; set; }
    }

    public class StockTurnoverSummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal AverageStockTurnover { get; set; }
        public string TurnoverCategory { get; set; } = string.Empty; // Fast, Medium, Slow
    }

    public class ProductProfitSummary
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal ProfitAfterDiscounts { get; set; }
    }

    public class CategoryProfitSummary
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }
        public decimal TotalDiscounts { get; set; }
        public int ProductsSold { get; set; }
    }

    public class DailyProfitSummary
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }
    }

    public class GSTRateSummary
    {
        public decimal GSTRate { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GSTAmount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class MonthlyGSTSummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal TaxableAmount { get; set; }
        public decimal GSTAmount { get; set; }
    }

    public class CustomerSegmentSummary
    {
        public string Segment { get; set; } = string.Empty; // New, Regular, VIP, etc.
        public int CustomerCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public class MonthlyCustomerSummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public int NewCustomers { get; set; }
        public int ActiveCustomers { get; set; }
    }

    public class ProductDiscountSummary
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal AverageDiscountPercentage { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public int TimesDiscounted { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal RevenueImpact { get; set; }
    }

    public class DailyDiscountSummary
    {
        public DateTime Date { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal AverageDiscountPercentage { get; set; }
        public int SalesWithDiscounts { get; set; }
        public decimal DiscountPenetration { get; set; }
    }

    public class CategoryPerformanceSummary
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; }
        public int ProductsSold { get; set; }
        public decimal AverageDiscount { get; set; }
        public decimal MarketShare { get; set; } // % of total revenue
    }

    public class CategoryTrendSummary
    {
        public string CategoryName { get; set; } = string.Empty;
        public List<DailySalesSummary> DailyTrends { get; set; } = new();
    }

    public class ReturnReasonSummary
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ProductReturnSummary
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal ReturnQuantity { get; set; }
        public decimal ReturnAmount { get; set; }
        public decimal ReturnRate { get; set; }
        public List<string> CommonReturnReasons { get; set; } = new();
    }

    public class DailyReturnSummary
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal ReturnRate { get; set; }
    }
}