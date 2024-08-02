using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_hierarchy",
                table: "LegalPeople",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "company_hierarchy",
                table: "LegalPeople");
        }
    }
}
