using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "class_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "make_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_make_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "type_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_type_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClassCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MakeCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModelCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookDepreciationCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BookDepreciationRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    StockDepreciationCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StockDepreciationRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    GuidelineLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_class_codes_ClassCodeId",
                        column: x => x.ClassCodeId,
                        principalTable: "class_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_make_codes_MakeCodeId",
                        column: x => x.MakeCodeId,
                        principalTable: "make_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_model_codes_ModelCodeId",
                        column: x => x.ModelCodeId,
                        principalTable: "model_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_type_codes_TypeCodeId",
                        column: x => x.TypeCodeId,
                        principalTable: "type_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "class_codes",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-00a1-000000000001"), "LAP", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Laptops & Computers", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a1-000000000002"), "FUR", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Furniture & Fixtures", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a1-000000000003"), "VEH", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Vehicles", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "make_codes",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-00a2-000000000001"), "DEL", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dell", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a2-000000000002"), "HP", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HP", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a2-000000000003"), "TAT", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tata", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "model_codes",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-00a4-000000000001"), "MDL1", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Model 2024 Series", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a4-000000000002"), "MDL2", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Model 2023 Series", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "type_codes",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-00a3-000000000001"), "HW", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Hardware", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a3-000000000002"), "OFF", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Office Equipment", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-00a3-000000000003"), "TRN", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Transport", true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "assets",
                columns: new[] { "Id", "AssetCode", "BookDepreciationCategory", "BookDepreciationRate", "ClassCodeId", "CreatedAt", "Description", "GuidelineLimit", "IsActive", "MakeCodeId", "ModelCodeId", "StockDepreciationCategory", "StockDepreciationRate", "TypeCodeId", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-00a5-000000000001"), "LAP-000001", "Straight Line", 25.00m, new Guid("00000000-0000-0000-00a1-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Standard Issue Laptop", 60000.00m, true, null, null, "WDV", 15.00m, new Guid("00000000-0000-0000-00a3-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_assets_AssetCode",
                table: "assets",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_ClassCodeId",
                table: "assets",
                column: "ClassCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_MakeCodeId",
                table: "assets",
                column: "MakeCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_ModelCodeId",
                table: "assets",
                column: "ModelCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_TypeCodeId",
                table: "assets",
                column: "TypeCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_class_codes_Code",
                table: "class_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_make_codes_Code",
                table: "make_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_codes_Code",
                table: "model_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_type_codes_Code",
                table: "type_codes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "class_codes");

            migrationBuilder.DropTable(
                name: "make_codes");

            migrationBuilder.DropTable(
                name: "model_codes");

            migrationBuilder.DropTable(
                name: "type_codes");
        }
    }
}
