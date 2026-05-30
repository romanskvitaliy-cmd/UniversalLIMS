using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE Branches SET Kind = 1
                WHERE UPPER(Code) LIKE 'REG-%' OR UPPER(Code) = 'REG';

                UPDATE Branches SET Kind = 2
                WHERE UPPER(Code) LIKE 'EXP-%' OR UPPER(Code) = 'EXP';

                UPDATE Branches SET Kind = 3
                WHERE UPPER(Code) LIKE 'MIX-%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Branches");
        }
    }
}
