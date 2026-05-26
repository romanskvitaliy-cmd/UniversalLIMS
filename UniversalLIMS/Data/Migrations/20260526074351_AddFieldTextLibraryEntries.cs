using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldTextLibraryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldTextLibraryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NormalizedTag = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    NormalizedBodyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ShortLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FieldTextLibraryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldTextLibraryEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldTextLibraryEntries_DataFields_DataFieldId",
                        column: x => x.DataFieldId,
                        principalTable: "DataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "DataFieldId", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_UsageCount",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "DataFieldId", "UsageCount" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "NormalizedTag", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NULL AND [NormalizedTag] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_DataFieldId",
                table: "FieldTextLibraryEntries",
                column: "DataFieldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldTextLibraryEntries");
        }
    }
}
