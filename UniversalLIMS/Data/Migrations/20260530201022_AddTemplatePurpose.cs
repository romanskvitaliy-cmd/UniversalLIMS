using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplatePurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Templates",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("""
                UPDATE Templates SET Purpose = 1
                WHERE UPPER(Code) LIKE 'REF-%'
                   OR UPPER(Code) LIKE 'REF[_]%'
                   OR UPPER(Code) = 'REF';

                UPDATE Templates SET Purpose = 3
                WHERE UPPER(Code) LIKE 'CONCLUSION-%'
                   OR UPPER(Code) LIKE 'CONCLUSION[_]%'
                   OR UPPER(Code) = 'CONCLUSION';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Templates");
        }
    }
}
