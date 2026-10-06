using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <summary>
    /// Data-only seed completing the end-to-end sample user lifecycle on top of the earlier
    /// SeedSampleUserAccess (which seeded the admin + usr001 access rows). Adds:
    ///   - a sample user group "Operations Team" (OPSGRP) with usr001 as a member;
    ///   - group-owned access rows (Query-only on LookupMaster + NationalityMaster);
    ///   - branch associations: admin -> ALL (IsAll, null LocationId); usr001 -> Fort Branch
    ///     (MUM-S-01) by LocationId. Both scoped to the Retail Lending LOB.
    /// Carries no schema change. Down() removes exactly the rows inserted here.
    /// </summary>
    public partial class SeedSampleUserGroupAndBranchAccess : Migration
    {
        // Existing seeded ids (from prior migrations/configs).
        private static readonly Guid AdminUserId = new("00000000-0000-0000-0002-000000000000");
        private static readonly Guid Usr001Id = new("00000000-0000-0000-0002-000000000001");
        private static readonly Guid RetailLendingLobId = new("00000000-0000-0000-0002-000000000001");
        private static readonly Guid LookupProgramId = new("00000000-0000-0000-0001-000000000001");
        private static readonly Guid NationalityProgramId = new("00000000-0000-0000-0001-000000000002");
        private static readonly Guid FortBranchLocationId = new("00000000-0000-0000-0000-000000000010");
        private const string RoleCenter = "System Admin";

        // New rows (distinct deterministic ranges so Down() is exact).
        private static readonly Guid GroupId = new("00000000-0000-0000-0005-000000000001");
        private static readonly Guid GroupMemberId = new("00000000-0000-0000-0006-000000000001");
        private static readonly Guid GroupAccessLookupId = new("00000000-0000-0000-0007-000000000001");
        private static readonly Guid GroupAccessNationalityId = new("00000000-0000-0000-0007-000000000002");
        private static readonly Guid BranchAdminAllId = new("00000000-0000-0000-0008-000000000001");
        private static readonly Guid BranchUsr001FortId = new("00000000-0000-0000-0008-000000000002");

        private static readonly DateTime Seeded = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static string RoleCode(string programName) => (RoleCenter + programName).ToUpperInvariant();

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) User group "Operations Team".
            migrationBuilder.InsertData(
                table: "user_groups",
                columns: new[] { "Id", "UserGroupCode", "Name", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[] { GroupId, "OPSGRP", "Operations Team", true, Seeded, Seeded });

            // 2) Group member: usr001.
            migrationBuilder.InsertData(
                table: "user_group_members",
                columns: new[] { "Id", "UserGroupId", "UserAccountId" },
                values: new object[] { GroupMemberId, GroupId, Usr001Id });

            // 3) Group-owned access rows (Query only), owner = group (UserAccountId null).
            var accessCols = new[]
            {
                "Id", "UserAccountId", "UserGroupId", "LineOfBusinessId", "RoleCenterName",
                "ProgramId", "RoleCode", "CanAdd", "CanModify", "CanQuery", "CanDelete"
            };
            migrationBuilder.InsertData(
                table: "user_access_assignments",
                columns: accessCols,
                values: new object[]
                {
                    GroupAccessLookupId, null, GroupId, RetailLendingLobId, RoleCenter,
                    LookupProgramId, RoleCode("LookupMaster"), false, false, true, false
                });
            migrationBuilder.InsertData(
                table: "user_access_assignments",
                columns: accessCols,
                values: new object[]
                {
                    GroupAccessNationalityId, null, GroupId, RetailLendingLobId, RoleCenter,
                    NationalityProgramId, RoleCode("NationalityMaster"), false, false, true, false
                });

            // 4) Branch associations.
            var branchCols = new[]
            {
                "Id", "UserAccountId", "UserGroupId", "LineOfBusinessId", "BranchCode", "IsAll", "LocationId"
            };
            // admin -> ALL (null LocationId).
            migrationBuilder.InsertData(
                table: "user_branch_associations",
                columns: branchCols,
                values: new object[]
                {
                    BranchAdminAllId, AdminUserId, null, RetailLendingLobId, "ALL", true, null
                });
            // usr001 -> Fort Branch (MUM-S-01) by LocationId.
            migrationBuilder.InsertData(
                table: "user_branch_associations",
                columns: branchCols,
                values: new object[]
                {
                    BranchUsr001FortId, Usr001Id, null, RetailLendingLobId, "MUM-S-01", false, FortBranchLocationId
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "user_branch_associations", keyColumn: "Id", keyValue: BranchAdminAllId);
            migrationBuilder.DeleteData(table: "user_branch_associations", keyColumn: "Id", keyValue: BranchUsr001FortId);
            migrationBuilder.DeleteData(table: "user_access_assignments", keyColumn: "Id", keyValue: GroupAccessLookupId);
            migrationBuilder.DeleteData(table: "user_access_assignments", keyColumn: "Id", keyValue: GroupAccessNationalityId);
            migrationBuilder.DeleteData(table: "user_group_members", keyColumn: "Id", keyValue: GroupMemberId);
            migrationBuilder.DeleteData(table: "user_groups", keyColumn: "Id", keyValue: GroupId);
        }
    }
}
