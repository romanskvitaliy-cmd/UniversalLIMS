using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateConstructor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookmarkPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateBookmarkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_BookmarkPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateBookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DataFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TemplateBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateBookmarks_DataFields_DataFieldId",
                        column: x => x.DataFieldId,
                        principalTable: "DataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameUk = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionUk = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentPublishedVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublicationNotesUk = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_TemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateVersions_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
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

            migrationBuilder.CreateIndex(
                name: "IX_Templates_Code",
                table: "Templates",
                column: "Code",
                unique: true,
                filter: "[IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_CurrentPublishedVersionId",
                table: "Templates",
                column: "CurrentPublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersions_TemplateId_Status",
                table: "TemplateVersions",
                columns: new[] { "TemplateId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersions_TemplateId_VersionNumber",
                table: "TemplateVersions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BookmarkPermissions_TemplateBookmarks_TemplateBookmarkId",
                table: "BookmarkPermissions",
                column: "TemplateBookmarkId",
                principalTable: "TemplateBookmarks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateBookmarks_TemplateVersions_TemplateVersionId",
                table: "TemplateBookmarks",
                column: "TemplateVersionId",
                principalTable: "TemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_TemplateVersions_CurrentPublishedVersionId",
                table: "Templates",
                column: "CurrentPublishedVersionId",
                principalTable: "TemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Templates_TemplateVersions_CurrentPublishedVersionId",
                table: "Templates");

            migrationBuilder.DropTable(
                name: "BookmarkPermissions");

            migrationBuilder.DropTable(
                name: "TemplateBookmarks");

            migrationBuilder.DropTable(
                name: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}
