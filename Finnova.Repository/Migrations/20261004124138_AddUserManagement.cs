using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "functional_group_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_functional_group_assignments", x => x.Id);
                    table.CheckConstraint("CK_functional_group_assignments_owner", "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "functional_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalGroupCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    RoleCenterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_functional_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DateOfJoining = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGroupCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_management_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordKind = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_management_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "functional_group_functions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_functional_group_functions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_functional_group_functions_functional_groups_FunctionalGroupId",
                        column: x => x.FunctionalGroupId,
                        principalTable: "functional_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_access_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineOfBusiness = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleCenterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProgramName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CanAdd = table.Column<bool>(type: "bit", nullable: false),
                    CanModify = table.Column<bool>(type: "bit", nullable: false),
                    CanQuery = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_access_assignments", x => x.Id);
                    table.CheckConstraint("CK_user_access_assignments_owner", "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_user_access_assignments_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_branch_associations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineOfBusiness = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsAll = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branch_associations", x => x.Id);
                    table.CheckConstraint("CK_user_branch_associations_owner", "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_user_branch_associations_user_accounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_group_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_group_members_user_groups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalTable: "user_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_functional_group_assignments_FunctionalGroupId_UserAccountId",
                table: "functional_group_assignments",
                columns: new[] { "FunctionalGroupId", "UserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_functional_group_assignments_FunctionalGroupId_UserGroupId",
                table: "functional_group_assignments",
                columns: new[] { "FunctionalGroupId", "UserGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_functional_group_functions_FunctionalGroupId_RoleCode",
                table: "functional_group_functions",
                columns: new[] { "FunctionalGroupId", "RoleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_functional_groups_FunctionalGroupCode",
                table: "functional_groups",
                column: "FunctionalGroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_UserAccountId_LineOfBusiness_RoleCode",
                table: "user_access_assignments",
                columns: new[] { "UserAccountId", "LineOfBusiness", "RoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_UserGroupId_LineOfBusiness_RoleCode",
                table: "user_access_assignments",
                columns: new[] { "UserGroupId", "LineOfBusiness", "RoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_Name",
                table: "user_accounts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_UserCode",
                table: "user_accounts",
                column: "UserCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_UserAccountId_LineOfBusiness",
                table: "user_branch_associations",
                columns: new[] { "UserAccountId", "LineOfBusiness" });

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_UserGroupId_LineOfBusiness",
                table: "user_branch_associations",
                columns: new[] { "UserGroupId", "LineOfBusiness" });

            migrationBuilder.CreateIndex(
                name: "IX_user_group_members_UserGroupId_UserAccountId",
                table: "user_group_members",
                columns: new[] { "UserGroupId", "UserAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_groups_Name",
                table: "user_groups",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_user_groups_UserGroupCode",
                table: "user_groups",
                column: "UserGroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_management_audit_RecordId_ChangedAtUtc",
                table: "user_management_audit",
                columns: new[] { "RecordId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "functional_group_assignments");

            migrationBuilder.DropTable(
                name: "functional_group_functions");

            migrationBuilder.DropTable(
                name: "user_access_assignments");

            migrationBuilder.DropTable(
                name: "user_branch_associations");

            migrationBuilder.DropTable(
                name: "user_group_members");

            migrationBuilder.DropTable(
                name: "user_management_audit");

            migrationBuilder.DropTable(
                name: "functional_groups");

            migrationBuilder.DropTable(
                name: "user_accounts");

            migrationBuilder.DropTable(
                name: "user_groups");
        }
    }
}
