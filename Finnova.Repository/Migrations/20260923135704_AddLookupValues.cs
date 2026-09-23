using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddLookupValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lookup_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LookupType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lookup_values", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "lookup_values",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayOrder", "IsActive", "IsSystemLocked", "LookupType", "Module", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "DEBIT", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, true, "SYS_TXN_TYPE", "SystemAdmin", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Debit" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "CREDIT", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, true, "SYS_TXN_TYPE", "SystemAdmin", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Credit" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "SIN", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, false, "MARITAL_STATUS", "Origination", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Single" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "MAR", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, false, "MARITAL_STATUS", "Origination", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Married" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "DIV", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, false, "MARITAL_STATUS", "Origination", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Divorced" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_lookup_values_Module_LookupType_Code",
                table: "lookup_values",
                columns: new[] { "Module", "LookupType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lookup_values_Module_LookupType_IsActive_DisplayOrder",
                table: "lookup_values",
                columns: new[] { "Module", "LookupType", "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lookup_values");
        }
    }
}
