using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <summary>
    /// Data-only seed of two sample access profiles into user_access_assignments:
    ///   - admin (UserAccount 00000000-0000-0000-0002-000000000000): one row per active program
    ///     (all 9), all four flags true — "access to all program menus". Note the admin ALSO
    ///     bypasses per-screen gating by role at /api/auth/me/permissions; these explicit rows make
    ///     the grant self-describing in the data.
    ///   - usr001 (UserAccount 00000000-0000-0000-0002-000000000001): a single row on LookupMaster
    ///     with Query only — "minimum program access".
    /// All rows are scoped to the "Retail Lending" LOB (lines_of_business 00000000-0000-0000-0002-000000000001)
    /// and the "System Admin" Role Center. RoleCode = (RoleCenterName + ProgramName).ToUpperInvariant()
    /// per RoleCodeBuilder. Program FK Ids come from ScreenProgramConfiguration (programs
    /// 00000000-0000-0000-0001-0000000000NN). This migration carries no schema change.
    /// </summary>
    public partial class SeedSampleUserAccess : Migration
    {
        private static readonly Guid AdminUserId = new("00000000-0000-0000-0002-000000000000");
        private static readonly Guid Usr001Id = new("00000000-0000-0000-0002-000000000001");
        private static readonly Guid RetailLendingLobId = new("00000000-0000-0000-0002-000000000001");
        private const string RoleCenter = "System Admin";

        // Access-row ids (deterministic) so Down() removes exactly these rows.
        // Admin rows: 00000000-0000-0000-0003-0000000000NN (NN = program ordinal 01..09).
        private static Guid AdminRow(int n) => new($"00000000-0000-0000-0003-0000000000{n:D2}");
        // Normal-user row.
        private static readonly Guid Usr001Row = new("00000000-0000-0000-0004-000000000001");

        // programs seed ids + names (ScreenProgramConfiguration).
        private static readonly (int Ord, string ProgramName)[] Programs =
        {
            (1, "LookupMaster"),
            (2, "NationalityMaster"),
            (3, "OrganizationHierarchy"),
            (4, "DocumentNumberControl"),
            (5, "CourtMaster"),
            (6, "EntityMaster"),
            (7, "UserManagement"),
            (8, "LocationMaster"),
            (9, "Organization"),
        };

        private static Guid ProgramId(int ord) => new($"00000000-0000-0000-0001-0000000000{ord:D2}");
        private static string RoleCode(string programName) => (RoleCenter + programName).ToUpperInvariant();

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var columns = new[]
            {
                "Id", "UserAccountId", "UserGroupId", "LineOfBusinessId", "RoleCenterName",
                "ProgramId", "RoleCode", "CanAdd", "CanModify", "CanQuery", "CanDelete"
            };

            // Admin: all 9 programs, all flags true.
            foreach (var (ord, programName) in Programs)
            {
                migrationBuilder.InsertData(
                    table: "user_access_assignments",
                    columns: columns,
                    values: new object[]
                    {
                        AdminRow(ord), AdminUserId, null, RetailLendingLobId, RoleCenter,
                        ProgramId(ord), RoleCode(programName), true, true, true, true
                    });
            }

            // usr001: minimum access — LookupMaster, Query only.
            migrationBuilder.InsertData(
                table: "user_access_assignments",
                columns: columns,
                values: new object[]
                {
                    Usr001Row, Usr001Id, null, RetailLendingLobId, RoleCenter,
                    ProgramId(1), RoleCode("LookupMaster"), false, false, true, false
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (ord, _) in Programs)
            {
                migrationBuilder.DeleteData(
                    table: "user_access_assignments",
                    keyColumn: "Id",
                    keyValue: AdminRow(ord));
            }

            migrationBuilder.DeleteData(
                table: "user_access_assignments",
                keyColumn: "Id",
                keyValue: Usr001Row);
        }
    }
}
