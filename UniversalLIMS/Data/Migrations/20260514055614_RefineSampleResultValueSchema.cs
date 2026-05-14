using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefineSampleResultValueSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateBookmarkName",
                table: "SampleResultValues");

            migrationBuilder.AlterColumn<string>(
                name: "StoredValue",
                table: "SampleResultValues",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EquipmentId",
                table: "SampleResultValues",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Uncertainty",
                table: "SampleResultValues",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "SampleResultValues",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Uncertainty",
                table: "SampleResultValues");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "SampleResultValues");

            migrationBuilder.AlterColumn<string>(
                name: "StoredValue",
                table: "SampleResultValues",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<Guid>(
                name: "EquipmentId",
                table: "SampleResultValues",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "TemplateBookmarkName",
                table: "SampleResultValues",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}
