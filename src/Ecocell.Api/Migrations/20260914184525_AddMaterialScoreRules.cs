using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialScoreRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterialScoreRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Material = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialScoreRules", x => x.Id);
                    table.CheckConstraint("CK_MaterialScoreRules_Points_Positive", "CAST(\"Points\" AS REAL) > 0");
                    table.CheckConstraint("CK_MaterialScoreRules_Validity", "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_MaterialScoreRules_LegalPeople_LegalPersonId",
                        column: x => x.LegalPersonId,
                        principalTable: "LegalPeople",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialScoreRules_LegalPersonId_Material",
                table: "MaterialScoreRules",
                columns: new[] { "LegalPersonId", "Material" },
                unique: true,
                filter: "\"ValidTo\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialScoreRules");
        }
    }
}
