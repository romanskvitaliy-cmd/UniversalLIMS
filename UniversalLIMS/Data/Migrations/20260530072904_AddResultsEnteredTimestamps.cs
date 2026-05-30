using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResultsEnteredTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsEnteredAtUtc",
                table: "Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsEnteredAtUtc",
                table: "OrderDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE d
                SET d.ResultsEnteredAtUtc = COALESCE(d.UpdatedAtUtc, d.CreatedAtUtc, SYSUTCDATETIME())
                FROM OrderDocuments d
                WHERE d.Status = 'ResultsEntered'
                  AND d.IsAnnulled = 0
                  AND d.ResultsEnteredAtUtc IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE s
                SET s.ResultsEnteredAtUtc = COALESCE(s.UpdatedAtUtc, s.CreatedAtUtc, SYSUTCDATETIME())
                FROM Samples s
                WHERE s.Status = 'ResultsEntered'
                  AND s.IsAnnulled = 0
                  AND s.ResultsEnteredAtUtc IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultsEnteredAtUtc",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ResultsEnteredAtUtc",
                table: "OrderDocuments");
        }
    }
}
