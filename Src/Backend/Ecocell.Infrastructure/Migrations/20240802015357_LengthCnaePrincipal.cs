using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LengthCnaePrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "principal_cnae",
                table: "LegalPeople",
                type: "varchar",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(10)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "principal_cnae",
                table: "LegalPeople",
                type: "varchar(10)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar");
        }
    }
}
