using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "organizations",
                columns: new[] { "Id", "AccountingCurrency", "CeoName", "Code", "CommunicationAddress", "CommunicationCity", "CommunicationCountry", "CommunicationPincode", "CommunicationState", "ConstitutionType", "CorporateAddress", "CorporateCity", "CorporateCountry", "CorporatePincode", "CorporateState", "CreatedAt", "Description", "Email", "GstNumber", "Mobile", "Name", "PanNumber", "RegistrationDate", "RegistrationNumber", "Status", "Telephone", "UpdatedAt", "Website" },
                values: new object[] { new Guid("00000000-0000-0000-0003-000000000001"), "INR", "Managing Director", "FINNOVA", "1 Finnova Tower, Bandra Kurla Complex", "Mumbai", "India", "400051", "Maharashtra", "PrivateLtd", "1 Finnova Tower, Bandra Kurla Complex", "Mumbai", "India", "400051", "Maharashtra", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Default organization for this Finnova instance.", "info@finnova.com", "27AAACF1234F1Z5", "9820010000", "Finnova Financial Services Ltd", "AAACF1234F", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "U65999MH2024PTC000001", "Active", "02261001000", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "https://www.finnova.com" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "organizations",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"));
        }
    }
}
