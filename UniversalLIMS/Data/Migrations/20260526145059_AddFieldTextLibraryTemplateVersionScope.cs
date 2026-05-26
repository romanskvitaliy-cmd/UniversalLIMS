using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldTextLibraryTemplateVersionScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_NormalizedBodyHash",
                table: "FieldTextLibraryEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateVersionId",
                table: "FieldTextLibraryEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "DataFieldId", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NOT NULL AND [TemplateVersionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_TemplateVersionId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "DataFieldId", "TemplateVersionId", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NOT NULL AND [TemplateVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "NormalizedTag", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NULL AND [NormalizedTag] IS NOT NULL AND [TemplateVersionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_TemplateVersionId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "NormalizedTag", "TemplateVersionId", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NULL AND [NormalizedTag] IS NOT NULL AND [TemplateVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_TemplateVersionId_DataFieldId",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "TemplateVersionId", "DataFieldId" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_TemplateVersionId",
                table: "FieldTextLibraryEntries",
                column: "TemplateVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldTextLibraryEntries_TemplateVersions_TemplateVersionId",
                table: "FieldTextLibraryEntries",
                column: "TemplateVersionId",
                principalTable: "TemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldTextLibraryEntries_TemplateVersions_TemplateVersionId",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_TemplateVersionId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_NormalizedBodyHash",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_TemplateVersionId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_TemplateVersionId_DataFieldId",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropIndex(
                name: "IX_FieldTextLibraryEntries_TemplateVersionId",
                table: "FieldTextLibraryEntries");

            migrationBuilder.DropColumn(
                name: "TemplateVersionId",
                table: "FieldTextLibraryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_DataFieldId_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "DataFieldId", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTextLibraryEntries_BranchId_NormalizedTag_NormalizedBodyHash",
                table: "FieldTextLibraryEntries",
                columns: new[] { "BranchId", "NormalizedTag", "NormalizedBodyHash" },
                unique: true,
                filter: "[IsAnnulled] = 0 AND [DataFieldId] IS NULL AND [NormalizedTag] IS NOT NULL");
        }
    }
}
