using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateFieldSegmentTextTypography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HorizontalAlignment",
                table: "TemplateFieldSegments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerticalAlignment",
                table: "TemplateFieldSegments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HorizontalAlignment",
                table: "TemplateFieldSegments");

            migrationBuilder.DropColumn(
                name: "VerticalAlignment",
                table: "TemplateFieldSegments");
        }
    }
}
