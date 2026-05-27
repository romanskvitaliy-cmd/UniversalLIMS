using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertConclusionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpertConclusionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SampleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    NotesUk = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertConclusionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertConclusionReviews_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertConclusionReviews_SampleId",
                table: "ExpertConclusionReviews",
                column: "SampleId",
                unique: true,
                filter: "[IsAnnulled] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertConclusionReviews");
        }
    }
}
