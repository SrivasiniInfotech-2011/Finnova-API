using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UnifyUserIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "user_accounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "user_accounts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"),
                columns: new[] { "Role", "UserName" },
                values: new object[] { "User", "usr001" });

            migrationBuilder.UpdateData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"),
                columns: new[] { "Role", "UserName" },
                values: new object[] { "User", "usr002" });

            migrationBuilder.UpdateData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"),
                columns: new[] { "Role", "UserName" },
                values: new object[] { "User", "usr003" });

            migrationBuilder.UpdateData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"),
                columns: new[] { "Role", "UserName" },
                values: new object[] { "User", "usr004" });

            migrationBuilder.UpdateData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"),
                columns: new[] { "Role", "UserName" },
                values: new object[] { "User", "usr005" });

            migrationBuilder.InsertData(
                table: "user_accounts",
                columns: new[] { "Id", "CreatedAt", "DateOfJoining", "Department", "Designation", "Email", "IsActive", "MobileNumber", "Name", "PasswordHash", "Role", "UpdatedAt", "UserCode", "UserName", "UserType" },
                values: new object[] { new Guid("00000000-0000-0000-0002-000000000000"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administration", "Administrator", "admin@finnova.com", true, null, "Admin User", "AQIDBAUGBwgJCgsMDQ4PEA==.7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=", "Admin", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ADMIN", "admin", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_UserName",
                table: "user_accounts",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_accounts_UserName",
                table: "user_accounts");

            migrationBuilder.DeleteData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000000"));

            migrationBuilder.DropColumn(
                name: "Role",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "user_accounts");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "Email", "FirstName", "LastName", "MiddleName", "OrganizationId", "PasswordHash", "Phone", "Role", "Status", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0001-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@finnova.com", "Admin", "User", null, null, "AQIDBAUGBwgJCgsMDQ4PEA==.7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=", null, "Admin", "Active", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_OrganizationId",
                table: "users",
                column: "OrganizationId");
        }
    }
}
