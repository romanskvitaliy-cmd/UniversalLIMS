using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateVersionPublicationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPublishedAtUtc",
                table: "TemplateVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepublishedAtUtc",
                table: "TemplateVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE TemplateVersions
                SET FirstPublishedAtUtc = PublishedAtUtc
                WHERE PublishedAtUtc IS NOT NULL AND FirstPublishedAtUtc IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstPublishedAtUtc",
                table: "TemplateVersions");

            migrationBuilder.DropColumn(
                name: "RepublishedAtUtc",
                table: "TemplateVersions");
        }
    }
}
