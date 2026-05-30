using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleDeliveryAndExpertRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "Samples",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAtUtc",
                table: "Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuedByUserId",
                table: "Samples",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyForPickupAtUtc",
                table: "Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedForReworkAtUtc",
                table: "ExpertConclusionReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedForReworkByUserId",
                table: "ExpertConclusionReviews",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReworkReasonUk",
                table: "ExpertConclusionReviews",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE s
                SET s.DeliveryStatus = 1,
                    s.ReadyForPickupAtUtc = COALESCE(r.ApprovedAtUtc, SYSUTCDATETIME())
                FROM Samples s
                INNER JOIN ExpertConclusionReviews r ON r.SampleId = s.Id
                WHERE r.Status = 2
                  AND s.IsAnnulled = 0
                  AND s.DeliveryStatus = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "IssuedAtUtc",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "IssuedByUserId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ReadyForPickupAtUtc",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ReturnedForReworkAtUtc",
                table: "ExpertConclusionReviews");

            migrationBuilder.DropColumn(
                name: "ReturnedForReworkByUserId",
                table: "ExpertConclusionReviews");

            migrationBuilder.DropColumn(
                name: "ReworkReasonUk",
                table: "ExpertConclusionReviews");
        }
    }
}
