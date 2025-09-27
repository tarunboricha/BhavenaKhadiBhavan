using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BhavenaKhadiBhavan.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    TotalPurchases = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastPurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FabricType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Pattern = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GSTRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    StockQuantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    MinimumStock = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GSTAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CalculatedTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentAdjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdjustmentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProcessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Returns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GSTAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Returns_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GSTRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    GSTAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ItemDiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    ItemDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItems", x => x.Id);
                    table.CheckConstraint("CK_SaleItem_ItemDiscountAmount", "[ItemDiscountAmount] >= 0");
                    table.CheckConstraint("CK_SaleItem_ItemDiscountPercentage", "[ItemDiscountPercentage] >= 0 AND [ItemDiscountPercentage] <= 100");
                    table.ForeignKey(
                        name: "FK_SaleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnId = table.Column<int>(type: "int", nullable: false),
                    SaleItemId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GSTRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    GSTAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnItems_Returns_ReturnId",
                        column: x => x.ReturnId,
                        principalTable: "Returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnItems_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9364), "Traditional kurtas for men", true, "Men's Kurtas" },
                    { 2, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9374), "Traditional kurtas for women", true, "Women's Kurtas" },
                    { 3, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9383), "Traditional dhotis", true, "Dhotis" },
                    { 4, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9392), "Traditional sarees", true, "Sarees" },
                    { 5, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9401), "Khadi shirts", true, "Shirts" },
                    { 6, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9410), "Khadi fabrics by meter", true, "Fabrics" },
                    { 7, new DateTime(2025, 9, 26, 21, 53, 15, 151, DateTimeKind.Local).AddTicks(9419), "Khadi accessories and bags", true, "Accessories" }
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "Address", "CreatedAt", "Email", "LastPurchaseDate", "Name", "Phone", "TotalOrders", "TotalPurchases" },
                values: new object[,]
                {
                    { 1, "456 MG Road, Mumbai, Maharashtra - 400001", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2757), "rajesh@example.com", null, "Rajesh Kumar", "9876543210", 0, 0m },
                    { 2, "789 Park Street, Delhi - 110001", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2761), "priya@example.com", null, "Priya Sharma", "9876543211", 0, 0m }
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "Category", "Description", "Key", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { 1, "Store", "Store name for invoices", "StoreName", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2266), "Bhavena Khadi Bhavan" },
                    { 2, "Store", "Store address", "StoreAddress", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2274), "Shop No 102, Viklang Mart, Nr. Water Tank, Kaliyabid, Bhavnagar, Gujarat - 364002" },
                    { 3, "Store", "Store phone number", "StorePhone", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2276), "+91 278-4051174" },
                    { 4, "Tax", "Store GST number", "GSTNumber", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2277), "27AAAAA0000A1Z5" },
                    { 5, "Store", "Invoice number prefix", "InvoicePrefix", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2279), "KHD" },
                    { 6, "Store", "Return number prefix", "ReturnPrefix", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2281), "RET" },
                    { 7, "Tax", "Default GST rate percentage", "DefaultGSTRate", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2282), "5.0" },
                    { 8, "Inventory", "Default low stock threshold", "LowStockThreshold", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2284), "5" },
                    { 9, "Store", "Store currency", "Currency", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2286), "INR" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FullName", "IsActive", "LastLogin", "PasswordHash", "Role", "Username" },
                values: new object[] { 1, new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(1591), "admin@khadistore.com", "Store Administrator", true, null, "$2a$11$b9t0QFJ7p2SPviY2tz13gOFiHrXF51D455pNpZU1OBEpWeKfTyh7W", "Admin", "admin" });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Color", "CreatedAt", "Description", "FabricType", "GSTRate", "IsActive", "MinimumStock", "Name", "Pattern", "PurchasePrice", "SKU", "SalePrice", "Size", "StockQuantity", "UnitOfMeasure", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 1, "White", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2385), "Pure cotton khadi kurta in white color", "Cotton Khadi", 5.0m, true, 5m, "Cotton Khadi Kurta", "Solid", 400m, "KHD-CK-W-M-001", 650m, "M", 25m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2385) },
                    { 2, 1, "White", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2393), "Pure cotton khadi kurta in white color", "Cotton Khadi", 5.0m, true, 5m, "Cotton Khadi Kurta", "Solid", 400m, "KHD-CK-W-L-002", 650m, "L", 20m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2394) },
                    { 3, 1, "White", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2400), "Pure cotton khadi kurta in white color", "Cotton Khadi", 5.0m, true, 5m, "Cotton Khadi Kurta", "Solid", 400m, "KHD-CK-W-XL-003", 650m, "XL", 15m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2400) },
                    { 4, 2, "Pink", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2406), "Cotton khadi kurta for women in pink", "Cotton Khadi", 5.0m, true, 8m, "Women's Khadi Kurta", "Printed", 380m, "KHD-WK-P-S-004", 580m, "S", 30m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2407) },
                    { 5, 2, "Pink", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2430), "Cotton khadi kurta for women in pink", "Cotton Khadi", 5.0m, true, 8m, "Women's Khadi Kurta", "Printed", 380m, "KHD-WK-P-M-005", 580m, "M", 25m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2446) },
                    { 6, 2, "Pink", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2465), "Cotton khadi kurta for women in pink", "Cotton Khadi", 5.0m, true, 8m, "Women's Khadi Kurta", "Printed", 380m, "KHD-WK-P-L-006", 580m, "L", 20m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2465) },
                    { 7, 4, "Blue", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2471), "Handwoven silk khadi saree in royal blue", "Silk Khadi", 5.0m, true, 3m, "Silk Khadi Saree", "Handloom", 1200m, "KHD-SS-B-001", 1800m, "Free Size", 15m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2472) },
                    { 8, 3, "Cream", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2477), "Pure cotton dhoti in cream color", "Cotton Khadi", 5.0m, true, 5m, "Traditional Dhoti", "Solid", 300m, "KHD-D-C-001", 480m, "Free Size", 20m, "Piece", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2478) },
                    { 9, 6, "Natural", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2489), "Pure khadi cotton fabric per meter", "Cotton Khadi", 5.0m, true, 20m, "Khadi Cotton Fabric", "Plain", 80m, "KHD-CF-N-001", 120m, "Per Meter", 100m, "Meter", new DateTime(2025, 9, 26, 21, 53, 15, 406, DateTimeKind.Local).AddTicks(2490) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive",
                table: "Categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SKU",
                table: "Products",
                column: "SKU");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnItems_ProductId",
                table: "ReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnItems_ReturnId",
                table: "ReturnItems",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnItems_SaleItemId",
                table: "ReturnItems",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_ReturnDate",
                table: "Returns",
                column: "ReturnDate");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_ReturnNumber",
                table: "Returns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Returns_SaleId",
                table: "Returns",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_Status",
                table: "Returns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ProductId",
                table: "SaleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleDate",
                table: "Sales",
                column: "SaleDate");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status",
                table: "Sales",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Category",
                table: "Settings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Key",
                table: "Settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReturnItems");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Returns");

            migrationBuilder.DropTable(
                name: "SaleItems");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
