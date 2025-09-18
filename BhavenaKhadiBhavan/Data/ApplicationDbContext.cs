using Microsoft.EntityFrameworkCore;
using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Data
{
    /// <summary>
    /// Simple Entity Framework DbContext for Khadi Store
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets for all entities
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Return> Returns { get; set; }
        public DbSet<ReturnItem> ReturnItems { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure decimal precision for Indian Rupee amounts
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.PurchasePrice).HasPrecision(18, 2);
                entity.Property(p => p.SalePrice).HasPrecision(18, 2);
                entity.Property(p => p.GSTRate).HasPrecision(5, 2);

                // Index for better performance
                entity.HasIndex(p => p.Name);
                entity.HasIndex(p => p.SKU);
                entity.HasIndex(p => p.IsActive);
                entity.HasIndex(p => p.CategoryId);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Name).IsUnique();
                entity.HasIndex(c => c.IsActive);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.Property(c => c.TotalPurchases).HasPrecision(18, 2);

                // Index for searching customers
                entity.HasIndex(c => c.Name);
                entity.HasIndex(c => c.Phone);
                entity.HasIndex(c => c.Email);
            });

            modelBuilder.Entity<Sale>(entity =>
            {
                entity.Property(s => s.SubTotal).HasPrecision(18, 2);
                entity.Property(s => s.GSTAmount).HasPrecision(18, 2);
                entity.Property(s => s.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(s => s.DiscountAmount).HasPrecision(18, 2);
                entity.Property(s => s.TotalAmount).HasPrecision(18, 2);

                // Unique invoice number
                entity.HasIndex(s => s.InvoiceNumber).IsUnique();
                entity.HasIndex(s => s.SaleDate);
                entity.HasIndex(s => s.CustomerId);
                entity.HasIndex(s => s.Status);
            });

            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.Property(si => si.UnitPrice).HasPrecision(18, 2);
                entity.Property(si => si.GSTRate).HasPrecision(5, 2);
                entity.Property(si => si.GSTAmount).HasPrecision(18, 2);
                entity.Property(si => si.LineTotal).HasPrecision(18, 2);
            });

            // CRITICAL FIX: Return Configuration with correct property names
            modelBuilder.Entity<Return>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReturnNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.RefundMethod).HasMaxLength(50).HasDefaultValue("Cash");
                entity.Property(e => e.RefundReference).HasMaxLength(100);
                entity.Property(e => e.ProcessedBy).HasMaxLength(100);

                // CRITICAL FIX: Correct property names for Return financial fields
                entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.TotalItemDiscounts).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.GSTAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.RefundAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);

                entity.HasIndex(e => e.ReturnNumber).IsUnique();
                entity.HasIndex(e => e.ReturnDate);
                entity.HasIndex(e => e.Status);

                entity.HasOne(e => e.Sale)
                      .WithMany(s => s.Returns) // Assuming you add this navigation property to Sale
                      .HasForeignKey(e => e.SaleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // CRITICAL FIX: ReturnItem Configuration with correct property names
            modelBuilder.Entity<ReturnItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ReturnQuantity).HasColumnType("decimal(10,3)");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GSTRate).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.GSTAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.UnitOfMeasure).HasMaxLength(20).HasDefaultValue("Piece");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.Condition).HasMaxLength(500);

                // CRITICAL FIX: Correct property names for proportional discount fields  
                entity.Property(e => e.OriginalItemDiscountPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.ProportionalDiscountAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);

                entity.HasOne(e => e.Return)
                      .WithMany(r => r.ReturnItems)
                      .HasForeignKey(e => e.ReturnId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.SaleItem)
                      .WithMany(si => si.ReturnItems) // Assuming you add this navigation property to SaleItem
                      .HasForeignKey(e => e.SaleItemId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.ReturnItems) // Assuming you add this navigation property to Product
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.IsActive);
            });

            modelBuilder.Entity<Setting>(entity =>
            {
                entity.HasIndex(s => s.Key).IsUnique();
                entity.HasIndex(s => s.Category);
            });

            // Configure relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.Return)
                .WithMany(r => r.ReturnItems)
                .HasForeignKey(ri => ri.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.SaleItem)
                .WithMany(si => si.ReturnItems)
                .HasForeignKey(ri => ri.SaleItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.Product)
                .WithMany()
                .HasForeignKey(ri => ri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed initial data
            SeedInitialData(modelBuilder);
        }

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            // Seed default categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Men's Kurtas", Description = "Traditional kurtas for men", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 2, Name = "Women's Kurtas", Description = "Traditional kurtas for women", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 3, Name = "Dhotis", Description = "Traditional dhotis", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 4, Name = "Sarees", Description = "Traditional sarees", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 5, Name = "Shirts", Description = "Khadi shirts", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 6, Name = "Fabrics", Description = "Khadi fabrics by meter", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 7, Name = "Accessories", Description = "Khadi accessories and bags", IsActive = true, CreatedAt = DateTime.Now }
            );

            // Seed default admin user (password: Admin@123)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    FullName = "Store Administrator",
                    Email = "admin@khadistore.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            );

            // Seed default settings
            modelBuilder.Entity<Setting>().HasData(
                new Setting { Id = 1, Key = "StoreName", Value = "Khadi Store", Description = "Store name for invoices", Category = "Store" },
                new Setting { Id = 2, Key = "StoreAddress", Value = "123 Gandhi Road, City, State - 400001", Description = "Store address", Category = "Store" },
                new Setting { Id = 3, Key = "StorePhone", Value = "+91 98765 43210", Description = "Store phone number", Category = "Store" },
                new Setting { Id = 4, Key = "StoreEmail", Value = "info@khadistore.com", Description = "Store email", Category = "Store" },
                new Setting { Id = 5, Key = "GSTNumber", Value = "27AAAAA0000A1Z5", Description = "Store GST number", Category = "Tax" },
                new Setting { Id = 6, Key = "InvoicePrefix", Value = "KHD", Description = "Invoice number prefix", Category = "Store" },
                new Setting { Id = 7, Key = "ReturnPrefix", Value = "RET", Description = "Return number prefix", Category = "Store" },
                new Setting { Id = 8, Key = "DefaultGSTRate", Value = "5.0", Description = "Default GST rate percentage", Category = "Tax" },
                new Setting { Id = 9, Key = "LowStockThreshold", Value = "5", Description = "Default low stock threshold", Category = "Inventory" },
                new Setting { Id = 10, Key = "Currency", Value = "INR", Description = "Store currency", Category = "Store" }
            );

            // Seed sample products
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Cotton Khadi Kurta - White",
                    Description = "Pure cotton khadi kurta in white color",
                    CategoryId = 1,
                    PurchasePrice = 400,
                    SalePrice = 650,
                    StockQuantity = 25,
                    MinimumStock = 5,
                    SKU = "KHD-CK-W-001",
                    FabricType = "Cotton Khadi",
                    Color = "White",
                    Size = "M",
                    Pattern = "Solid",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 2,
                    Name = "Silk Khadi Saree - Blue",
                    Description = "Handwoven silk khadi saree in royal blue",
                    CategoryId = 4,
                    PurchasePrice = 1200,
                    SalePrice = 1800,
                    StockQuantity = 15,
                    MinimumStock = 3,
                    SKU = "KHD-SS-B-001",
                    FabricType = "Silk Khadi",
                    Color = "Blue",
                    Size = "Free Size",
                    Pattern = "Handloom",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 3,
                    Name = "Traditional Dhoti - Cream",
                    Description = "Pure cotton dhoti in cream color",
                    CategoryId = 3,
                    PurchasePrice = 300,
                    SalePrice = 480,
                    StockQuantity = 20,
                    MinimumStock = 5,
                    SKU = "KHD-D-C-001",
                    FabricType = "Cotton Khadi",
                    Color = "Cream",
                    Size = "Free Size",
                    Pattern = "Solid",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 4,
                    Name = "Women's Khadi Kurta - Pink",
                    Description = "Cotton khadi kurta for women in pink",
                    CategoryId = 2,
                    PurchasePrice = 380,
                    SalePrice = 580,
                    StockQuantity = 30,
                    MinimumStock = 8,
                    SKU = "KHD-WK-P-001",
                    FabricType = "Cotton Khadi",
                    Color = "Pink",
                    Size = "L",
                    Pattern = "Printed",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 5,
                    Name = "Khadi Cotton Fabric",
                    Description = "Pure khadi cotton fabric per meter",
                    CategoryId = 6,
                    PurchasePrice = 80,
                    SalePrice = 120,
                    StockQuantity = 100,
                    MinimumStock = 20,
                    SKU = "KHD-CF-N-001",
                    FabricType = "Cotton Khadi",
                    Color = "Natural",
                    Size = "Per Meter",
                    Pattern = "Plain",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }
            );

            // Seed sample customer
            modelBuilder.Entity<Customer>().HasData(
                new Customer
                {
                    Id = 1,
                    Name = "Rajesh Kumar",
                    Phone = "9876543210",
                    Email = "rajesh@example.com",
                    Address = "456 MG Road, Mumbai, Maharashtra - 400001",
                    TotalOrders = 0,
                    TotalPurchases = 0,
                    CreatedAt = DateTime.Now
                },
                new Customer
                {
                    Id = 2,
                    Name = "Priya Sharma",
                    Phone = "9876543211",
                    Email = "priya@example.com",
                    Address = "789 Park Street, Delhi - 110001",
                    TotalOrders = 0,
                    TotalPurchases = 0,
                    CreatedAt = DateTime.Now
                }
            );
        }
    }

    /// <summary>
    /// Database initialization helper
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Apply any pending migrations
            if (context.Database.GetPendingMigrations().Any())
            {
                context.Database.Migrate();
            }
        }

        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Apply any pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await context.Database.MigrateAsync();
            }
        }

        public static void SeedTestData(ApplicationDbContext context)
        {
            // Add any additional test data if needed
            if (!context.Sales.Any())
            {
                // Add sample sales data for testing
                var testSale = new Sale
                {
                    InvoiceNumber = "KHD000001",
                    SaleDate = DateTime.Today.AddDays(-1),
                    CustomerId = 1,
                    PaymentMethod = "Cash",
                    SubTotal = 650,
                    GSTAmount = 32.50m,
                    DiscountPercentage = 0,
                    DiscountAmount = 0,
                    TotalAmount = 682.50m,
                    Status = "Completed"
                };

                context.Sales.Add(testSale);
                context.SaveChanges();

                var testSaleItem = new SaleItem
                {
                    SaleId = testSale.Id,
                    ProductId = 1,
                    ProductName = "Cotton Khadi Kurta - White",
                    Quantity = 1,
                    UnitPrice = 650,
                    GSTRate = 5.0m,
                    GSTAmount = 32.50m,
                    LineTotal = 682.50m
                };

                context.SaleItems.Add(testSaleItem);
                context.SaveChanges();

                // Update customer totals
                var customer = context.Customers.Find(1);
                if (customer != null)
                {
                    customer.TotalOrders = 1;
                    customer.TotalPurchases = 682.50m;
                    customer.LastPurchaseDate = DateTime.Today.AddDays(-1);
                    context.SaveChanges();
                }
            }
        }
    }
}