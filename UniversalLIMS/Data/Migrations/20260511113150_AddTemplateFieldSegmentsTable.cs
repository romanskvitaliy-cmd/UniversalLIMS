using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateFieldSegmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnTemplateVersionId",
                table: "TemplateVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FieldType",
                table: "TemplateFields",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.CreateTable(
                name: "TemplateFieldSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    PositionX = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    PositionY = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    Width = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    Height = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    TextAlignment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FontName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FontSize = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    LineHeight = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    SvgPathData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateFieldSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFieldSegments_TemplateFields_TemplateFieldId",
                        column: x => x.TemplateFieldId,
                        principalTable: "TemplateFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFieldSegments_TemplateFieldId_Sequence",
                table: "TemplateFieldSegments",
                columns: new[] { "TemplateFieldId", "Sequence" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [TemplateFieldSegments] (
                    [Id],
                    [TemplateFieldId],
                    [Sequence],
                    [PageNumber],
                    [PositionX],
                    [PositionY],
                    [Width],
                    [Height],
                    [IsPrimary],
                    [TextAlignment],
                    [CreatedAtUtc],
                    [CreatedByUserId])
                SELECT
                    NEWID(),
                    [tf].[Id],
                    1,
                    COALESCE([tf].[PageNumber], 1),
                    COALESCE([tf].[PositionX], 24),
                    COALESCE([tf].[PositionY], 24),
                    COALESCE([tf].[Width], 220),
                    COALESCE([tf].[Height], 28),
                    CAST(1 AS bit),
                    N'Left',
                    COALESCE([tf].[CreatedAtUtc], SYSUTCDATETIME()),
                    [tf].[CreatedByUserId]
                FROM [TemplateFields] AS [tf];
                """);

            migrationBuilder.DropColumn(
                name: "Height",
                table: "TemplateFields");

            migrationBuilder.DropColumn(
                name: "PageNumber",
                table: "TemplateFields");

            migrationBuilder.DropColumn(
                name: "PositionX",
                table: "TemplateFields");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "TemplateFields");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "TemplateFields");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersions_BasedOnTemplateVersionId",
                table: "TemplateVersions",
                column: "BasedOnTemplateVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateVersions_TemplateVersions_BasedOnTemplateVersionId",
                table: "TemplateVersions",
                column: "BasedOnTemplateVersionId",
                principalTable: "TemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TemplateVersions_TemplateVersions_BasedOnTemplateVersionId",
                table: "TemplateVersions");

            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                table: "TemplateFields",
                type: "decimal(12,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageNumber",
                table: "TemplateFields",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionX",
                table: "TemplateFields",
                type: "decimal(12,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionY",
                table: "TemplateFields",
                type: "decimal(12,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Width",
                table: "TemplateFields",
                type: "decimal(12,4)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [tf]
                SET
                    [tf].[PageNumber] = [segment].[PageNumber],
                    [tf].[PositionX] = [segment].[PositionX],
                    [tf].[PositionY] = [segment].[PositionY],
                    [tf].[Width] = [segment].[Width],
                    [tf].[Height] = [segment].[Height]
                FROM [TemplateFields] AS [tf]
                INNER JOIN [TemplateFieldSegments] AS [segment]
                    ON [segment].[TemplateFieldId] = [tf].[Id]
                    AND [segment].[IsPrimary] = 1;
                """);

            migrationBuilder.DropTable(
                name: "TemplateFieldSegments");

            migrationBuilder.DropIndex(
                name: "IX_TemplateVersions_BasedOnTemplateVersionId",
                table: "TemplateVersions");

            migrationBuilder.DropColumn(
                name: "BasedOnTemplateVersionId",
                table: "TemplateVersions");

            migrationBuilder.DropColumn(
                name: "FieldType",
                table: "TemplateFields");
        }
    }
}
