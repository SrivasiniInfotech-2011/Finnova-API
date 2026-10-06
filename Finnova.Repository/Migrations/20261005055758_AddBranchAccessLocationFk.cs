using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchAccessLocationFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "user_branch_associations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_associations_LocationId",
                table: "user_branch_associations",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_branch_associations_locations_LocationId",
                table: "user_branch_associations",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_branch_associations_locations_LocationId",
                table: "user_branch_associations");

            migrationBuilder.DropIndex(
                name: "IX_user_branch_associations_LocationId",
                table: "user_branch_associations");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "user_branch_associations");
        }
    }
}
