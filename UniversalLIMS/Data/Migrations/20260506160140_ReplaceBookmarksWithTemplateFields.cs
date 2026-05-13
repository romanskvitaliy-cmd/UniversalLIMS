using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBookmarksWithTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookmarkPermissions");

            migrationBuilder.DropTable(
                name: "TemplateBookmarks");

            migrationBuilder.AddColumn<string>(
                name: "ExampleValue",
                table: "DataFields",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "DataFields",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLength",
                table: "DataFields",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationRegex",
                table: "DataFields",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemplateFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedTag = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WordControlType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    EstimatedCapacityChars = table.Column<int>(type: "int", nullable: true),
                    MaxLines = table.Column<int>(type: "int", nullable: true),
                    AllowMultiline = table.Column<bool>(type: "bit", nullable: false),
                    OverflowPolicy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    PositionX = table.Column<decimal>(type: "decimal(12,4)", nullable: true),
                    PositionY = table.Column<decimal>(type: "decimal(12,4)", nullable: true),
                    Width = table.Column<decimal>(type: "decimal(12,4)", nullable: true),
                    Height = table.Column<decimal>(type: "decimal(12,4)", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMappedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMappedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_TemplateFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFields_DataFields_DataFieldId",
                        column: x => x.DataFieldId,
                        principalTable: "DataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemplateFields_TemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "TemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateFieldPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AccessLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_TemplateFieldPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFieldPermissions_TemplateFields_TemplateFieldId",
                        column: x => x.TemplateFieldId,
                        principalTable: "TemplateFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFieldPermissions_RoleName_AccessLevel",
                table: "TemplateFieldPermissions",
                columns: new[] { "RoleName", "AccessLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFieldPermissions_TemplateFieldId_RoleName",
                table: "TemplateFieldPermissions",
                columns: new[] { "TemplateFieldId", "RoleName" },
                unique: true,
                filter: "[IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFields_DataFieldId",
                table: "TemplateFields",
                column: "DataFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFields_TemplateVersionId_NormalizedTag",
                table: "TemplateFields",
                columns: new[] { "TemplateVersionId", "NormalizedTag" },
                unique: true,
                filter: "[IsAnnulled] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateFieldPermissions");

            migrationBuilder.DropTable(
                name: "TemplateFields");

            migrationBuilder.DropColumn(
                name: "ExampleValue",
                table: "DataFields");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "DataFields");

            migrationBuilder.DropColumn(
                name: "MaxLength",
                table: "DataFields");

            migrationBuilder.DropColumn(
                name: "ValidationRegex",
                table: "DataFields");

            migrationBuilder.CreateTable(
                name: "TemplateBookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    LastMappedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMappedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateBookmarks_DataFields_DataFieldId",
                        column: x => x.DataFieldId,
                        principalTable: "DataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemplateBookmarks_TemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "TemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateBookmarkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookmarkPermissions_TemplateBookmarks_TemplateBookmarkId",
                        column: x => x.TemplateBookmarkId,
                        principalTable: "TemplateBookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkPermissions_RoleName_AccessLevel",
                table: "BookmarkPermissions",
                columns: new[] { "RoleName", "AccessLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkPermissions_TemplateBookmarkId_RoleName",
                table: "BookmarkPermissions",
                columns: new[] { "TemplateBookmarkId", "RoleName" },
                unique: true,
                filter: "[IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateBookmarks_DataFieldId",
                table: "TemplateBookmarks",
                column: "DataFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateBookmarks_TemplateVersionId_NormalizedName",
                table: "TemplateBookmarks",
                columns: new[] { "TemplateVersionId", "NormalizedName" },
                unique: true,
                filter: "[IsAnnulled] = 0");
        }
    }
}
