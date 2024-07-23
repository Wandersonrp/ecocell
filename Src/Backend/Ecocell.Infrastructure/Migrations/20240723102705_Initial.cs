using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    street = table.Column<string>(type: "varchar(100)", nullable: false),
                    number = table.Column<string>(type: "varchar(15)", nullable: false),
                    complement = table.Column<string>(type: "varchar(50)", nullable: true),
                    neighborhood = table.Column<string>(type: "varchar(50)", nullable: false),
                    zip_code = table.Column<string>(type: "varchar(8)", nullable: false),
                    city = table.Column<string>(type: "varchar(100)", nullable: false),
                    state = table.Column<string>(type: "varchar(50)", nullable: false),
                    country = table.Column<string>(type: "varchar(50)", nullable: false),
                    latitude = table.Column<string>(type: "varchar", nullable: false),
                    longitude = table.Column<string>(type: "varchar", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_address", x => x.address_id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "varchar(14)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document", x => x.document_id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", nullable: false),
                    email = table.Column<string>(type: "varchar(100)", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_users_documents",
                        column: x => x.document_id,
                        principalTable: "Documents",
                        principalColumn: "document_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LegalPeople",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corporate_name = table.Column<string>(type: "varchar(100)", nullable: false),
                    company_description = table.Column<string>(type: "varchar(200)", nullable: false),
                    company_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", nullable: false),
                    principal_cnae = table.Column<string>(type: "varchar(10)", nullable: false),
                    company_status = table.Column<string>(type: "text", nullable: false),
                    is_discarding = table.Column<bool>(type: "boolean", nullable: false),
                    is_collector = table.Column<bool>(type: "boolean", nullable: false),
                    is_discarding_point = table.Column<bool>(type: "boolean", nullable: false),
                    address_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalPeople", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_LegalPeople_Addresses_address_id",
                        column: x => x.address_id,
                        principalTable: "Addresses",
                        principalColumn: "address_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_legal_person_user",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NaturalPeople",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_discarding = table.Column<bool>(type: "boolean", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NaturalPeople", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_natural_person_user",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalPeople_address_id",
                table: "LegalPeople",
                column: "address_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_document_id",
                table: "Users",
                column: "document_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalPeople");

            migrationBuilder.DropTable(
                name: "NaturalPeople");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Documents");
        }
    }
}
