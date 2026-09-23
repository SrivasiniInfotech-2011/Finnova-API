using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBranchesWithLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the old flat Branch module table.
            migrationBuilder.DropTable(
                name: "branches");

            // Create the hierarchical locations table.
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_locations_locations_ParentId",
                        column: x => x.ParentId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "locations",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Latitude", "Level", "Longitude", "Name", "ParentId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "IND", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Republic of India", true, null, 1, null, "India", null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "MH", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "State of Maharashtra", true, null, 2, null, "Maharashtra", new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "KA", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "State of Karnataka", true, null, 2, null, "Karnataka", new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "MUM", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Financial Capital", true, null, 3, null, "Mumbai", new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "PUN", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Cultural Capital of Maharashtra", true, null, 3, null, "Pune", new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "BLR", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Silicon Valley of India", true, null, 3, null, "Bangalore", new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "MUM-S", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "South Zone", true, null, 4, null, "South Mumbai", new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "MUM-W", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Western Zone", true, null, 4, null, "Western Mumbai", new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "PUN-C", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Central Zone", true, null, 4, null, "Central Pune", new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000010"), "MUM-S-01", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fort Area Branch Office", true, null, 5, null, "Fort Branch", new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000011"), "MUM-S-02", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Colaba Area Branch Office", false, null, 5, null, "Colaba Branch", new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "MUM-W-01", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Andheri West Branch Office", true, null, 5, null, "Andheri Branch", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "PUN-C-01", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Shivajinagar Branch Office", true, null, 5, null, "Shivajinagar Branch", new Guid("00000000-0000-0000-0000-000000000009"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_locations_ParentId_Code",
                table: "locations",
                columns: new[] { "ParentId", "Code" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "locations");

            // Recreate the old flat branches table (mirror of the original schema).
            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorporateCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StateCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Landmark = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "India"),
                    Pincode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsOperational = table.Column<bool>(type: "bit", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branches_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branches_CorporateCode_StateCode_BranchCode",
                table: "branches",
                columns: new[] { "CorporateCode", "StateCode", "BranchCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_OrganizationId",
                table: "branches",
                column: "OrganizationId");
        }
    }
}
