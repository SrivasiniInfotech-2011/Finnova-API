using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SeedUserAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "user_accounts",
                columns: new[] { "Id", "CreatedAt", "DateOfJoining", "Department", "Designation", "Email", "IsActive", "MobileNumber", "Name", "PasswordHash", "UpdatedAt", "UserCode", "UserType" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Operations", "Manager", "rohan.mehta@finnova.com", true, "9820012345", "Rohan Mehta", "AQIDBAUGBwgJCgsMDQ4PEA==.65aXkOppO1PIxiPNP45E/WmbZbtGN3dbC+lchI1ZTh8=", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USR001", 0 },
                    { new Guid("00000000-0000-0000-0002-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Finance", "Officer", "priya.nair@finnova.com", true, "9845023456", "Priya Nair", "AQIDBAUGBwgJCgsMDQ4PEA==.65aXkOppO1PIxiPNP45E/WmbZbtGN3dbC+lchI1ZTh8=", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USR002", 0 },
                    { new Guid("00000000-0000-0000-0002-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Retail Banking", "Teller", "arjun.rao@finnova.com", true, "9731034567", "Arjun Rao", "AQIDBAUGBwgJCgsMDQ4PEA==.65aXkOppO1PIxiPNP45E/WmbZbtGN3dbC+lchI1ZTh8=", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USR003", 1 },
                    { new Guid("00000000-0000-0000-0002-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Retail Banking", "Branch Head", "sneha.gupta@finnova.com", true, "9920045678", "Sneha Gupta", "AQIDBAUGBwgJCgsMDQ4PEA==.65aXkOppO1PIxiPNP45E/WmbZbtGN3dbC+lchI1ZTh8=", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USR004", 1 },
                    { new Guid("00000000-0000-0000-0002-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Risk", "Analyst", null, false, null, "Vikram Shah", "AQIDBAUGBwgJCgsMDQ4PEA==.65aXkOppO1PIxiPNP45E/WmbZbtGN3dbC+lchI1ZTh8=", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USR005", 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.DeleteData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"));

            migrationBuilder.DeleteData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"));

            migrationBuilder.DeleteData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"));

            migrationBuilder.DeleteData(
                table: "user_accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"));
        }
    }
}
