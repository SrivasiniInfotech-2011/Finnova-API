using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddLineOfBusinessAndFkNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_branch_associations_UserAccountId_LineOfBusiness",
                table: "user_branch_associations");

            migrationBuilder.DropIndex(
                name: "IX_user_branch_associations_UserGroupId_LineOfBusiness",
                table: "user_branch_associations");

            migrationBuilder.DropIndex(
                name: "IX_user_access_assignments_UserAccountId_LineOfBusiness_RoleCode",
                table: "user_access_assignments");

            migrationBuilder.DropIndex(
                name: "IX_user_access_assignments_UserGroupId_LineOfBusiness_RoleCode",
                table: "user_access_assignments");

            migrationBuilder.DropColumn(
                name: "LineOfBusiness",
                table: "user_branch_associations");

            migrationBuilder.DropColumn(
                name: "LineOfBusiness",
                table: "user_access_assignments");

            migrationBuilder.DropColumn(
                name: "ProgramName",
                table: "user_access_assignments");

            migrationBuilder.DropColumn(
                name: "ProgramName",
                table: "functional_group_functions");

            migrationBuilder.AddColumn<Guid>(
                name: "LineOfBusinessId",
                table: "user_branch_associations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LineOfBusinessId",
                table: "user_access_assignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "user_access_assignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "functional_group_functions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "lines_of_business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LOB_Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LOB_Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lines_of_business", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "lines_of_business",
                columns: new[] { "Id", "CreatedAt", "IsActive", "LOB_Description", "LOB_Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Loans and credit products for individual retail customers.", "Retail Lending", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0002-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Credit facilities for corporate and business customers.", "Corporate Lending", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0002-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Asset leasing and hire-purchase finance.", "Leasing", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_LineOfBusinessId",
                table: "user_branch_associations",
                column: "LineOfBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_UserAccountId_LineOfBusinessId",
                table: "user_branch_associations",
                columns: new[] { "UserAccountId", "LineOfBusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_UserGroupId_LineOfBusinessId",
                table: "user_branch_associations",
                columns: new[] { "UserGroupId", "LineOfBusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_LineOfBusinessId",
                table: "user_access_assignments",
                column: "LineOfBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_ProgramId",
                table: "user_access_assignments",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_UserAccountId_LineOfBusinessId_RoleCode",
                table: "user_access_assignments",
                columns: new[] { "UserAccountId", "LineOfBusinessId", "RoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_UserGroupId_LineOfBusinessId_RoleCode",
                table: "user_access_assignments",
                columns: new[] { "UserGroupId", "LineOfBusinessId", "RoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_functional_group_functions_ProgramId",
                table: "functional_group_functions",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_lines_of_business_LOB_Name",
                table: "lines_of_business",
                column: "LOB_Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_functional_group_functions_programs_ProgramId",
                table: "functional_group_functions",
                column: "ProgramId",
                principalTable: "programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_access_assignments_lines_of_business_LineOfBusinessId",
                table: "user_access_assignments",
                column: "LineOfBusinessId",
                principalTable: "lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_access_assignments_programs_ProgramId",
                table: "user_access_assignments",
                column: "ProgramId",
                principalTable: "programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_branch_associations_lines_of_business_LineOfBusinessId",
                table: "user_branch_associations",
                column: "LineOfBusinessId",
                principalTable: "lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_functional_group_functions_programs_ProgramId",
                table: "functional_group_functions");

            migrationBuilder.DropForeignKey(
                name: "FK_user_access_assignments_lines_of_business_LineOfBusinessId",
                table: "user_access_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_user_access_assignments_programs_ProgramId",
                table: "user_access_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_user_branch_associations_lines_of_business_LineOfBusinessId",
                table: "user_branch_associations");

            migrationBuilder.DropTable(
                name: "lines_of_business");

            migrationBuilder.DropIndex(
                name: "IX_user_branch_associations_LineOfBusinessId",
                table: "user_branch_associations");

            migrationBuilder.DropIndex(
                name: "IX_user_branch_associations_UserAccountId_LineOfBusinessId",
                table: "user_branch_associations");

            migrationBuilder.DropIndex(
                name: "IX_user_branch_associations_UserGroupId_LineOfBusinessId",
                table: "user_branch_associations");

            migrationBuilder.DropIndex(
                name: "IX_user_access_assignments_LineOfBusinessId",
                table: "user_access_assignments");

            migrationBuilder.DropIndex(
                name: "IX_user_access_assignments_ProgramId",
                table: "user_access_assignments");

            migrationBuilder.DropIndex(
                name: "IX_user_access_assignments_UserAccountId_LineOfBusinessId_RoleCode",
                table: "user_access_assignments");

            migrationBuilder.DropIndex(
                name: "IX_user_access_assignments_UserGroupId_LineOfBusinessId_RoleCode",
                table: "user_access_assignments");

            migrationBuilder.DropIndex(
                name: "IX_functional_group_functions_ProgramId",
                table: "functional_group_functions");

            migrationBuilder.DropColumn(
                name: "LineOfBusinessId",
                table: "user_branch_associations");

            migrationBuilder.DropColumn(
                name: "LineOfBusinessId",
                table: "user_access_assignments");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "user_access_assignments");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "functional_group_functions");

            migrationBuilder.AddColumn<string>(
                name: "LineOfBusiness",
                table: "user_branch_associations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LineOfBusiness",
                table: "user_access_assignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProgramName",
                table: "user_access_assignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProgramName",
                table: "functional_group_functions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_UserAccountId_LineOfBusiness",
                table: "user_branch_associations",
                columns: new[] { "UserAccountId", "LineOfBusiness" });

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_UserGroupId_LineOfBusiness",
                table: "user_branch_associations",
                columns: new[] { "UserGroupId", "LineOfBusiness" });

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_UserAccountId_LineOfBusiness_RoleCode",
                table: "user_access_assignments",
                columns: new[] { "UserAccountId", "LineOfBusiness", "RoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_user_access_assignments_UserGroupId_LineOfBusiness_RoleCode",
                table: "user_access_assignments",
                columns: new[] { "UserGroupId", "LineOfBusiness", "RoleCode" });
        }
    }
}
