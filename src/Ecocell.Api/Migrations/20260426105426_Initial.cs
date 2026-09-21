using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Journey = table.Column<int>(type: "integer", nullable: false),
                    PersonType = table.Column<int>(type: "integer", nullable: false),
                    PersonStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.PersonId);
                });

            migrationBuilder.CreateTable(
                name: "NaturalPeople",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NaturalPeople", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_NaturalPeople_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LegalPeople",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Cnae = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    ResponsiblePersonId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalPeople", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_LegalPeople_NaturalPeople_ResponsiblePersonId",
                        column: x => x.ResponsiblePersonId,
                        principalTable: "NaturalPeople",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LegalPeople_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalPeople_Cnpj",
                table: "LegalPeople",
                column: "Cnpj");

            migrationBuilder.CreateIndex(
                name: "IX_LegalPeople_ResponsiblePersonId",
                table: "LegalPeople",
                column: "ResponsiblePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_NaturalPeople_Cpf",
                table: "NaturalPeople",
                column: "Cpf");

            migrationBuilder.CreateIndex(
                name: "IX_People_Email",
                table: "People",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalPeople");

            migrationBuilder.DropTable(
                name: "NaturalPeople");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
