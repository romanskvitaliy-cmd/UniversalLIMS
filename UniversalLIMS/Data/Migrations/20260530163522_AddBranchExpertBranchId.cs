using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchExpertBranchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExpertBranchId",
                table: "Branches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_ExpertBranchId",
                table: "Branches",
                column: "ExpertBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Branches_ExpertBranchId",
                table: "Branches",
                column: "ExpertBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Branches_ExpertBranchId",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Branches_ExpertBranchId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ExpertBranchId",
                table: "Branches");
        }
    }
}
