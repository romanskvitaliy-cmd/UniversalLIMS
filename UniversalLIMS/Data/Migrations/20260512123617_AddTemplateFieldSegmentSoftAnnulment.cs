using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateFieldSegmentSoftAnnulment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemplateFieldSegments_TemplateFieldId_Sequence",
                table: "TemplateFieldSegments");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnulledAtUtc",
                table: "TemplateFieldSegments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnulledByUserId",
                table: "TemplateFieldSegments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnulmentReason",
                table: "TemplateFieldSegments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnnulled",
                table: "TemplateFieldSegments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFieldSegments_TemplateFieldId_Sequence",
                table: "TemplateFieldSegments",
                columns: new[] { "TemplateFieldId", "Sequence" },
                unique: true,
                filter: "[IsAnnulled] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemplateFieldSegments_TemplateFieldId_Sequence",
                table: "TemplateFieldSegments");

            migrationBuilder.DropColumn(
                name: "AnnulledAtUtc",
                table: "TemplateFieldSegments");

            migrationBuilder.DropColumn(
                name: "AnnulledByUserId",
                table: "TemplateFieldSegments");

            migrationBuilder.DropColumn(
                name: "AnnulmentReason",
                table: "TemplateFieldSegments");

            migrationBuilder.DropColumn(
                name: "IsAnnulled",
                table: "TemplateFieldSegments");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFieldSegments_TemplateFieldId_Sequence",
                table: "TemplateFieldSegments",
                columns: new[] { "TemplateFieldId", "Sequence" },
                unique: true);
        }
    }
}
