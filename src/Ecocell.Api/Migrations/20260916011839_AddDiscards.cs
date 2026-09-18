using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Discards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discards_LegalPeople_CollectorPointId",
                        column: x => x.CollectorPointId,
                        principalTable: "LegalPeople",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Discards_NaturalPeople_DepositorId",
                        column: x => x.DepositorId,
                        principalTable: "NaturalPeople",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscardItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Material = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ApproximateWeightKg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    MaterialScoreRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscardItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscardItems_Discards_DiscardId",
                        column: x => x.DiscardId,
                        principalTable: "Discards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscardItems_MaterialScoreRules_MaterialScoreRuleId",
                        column: x => x.MaterialScoreRuleId,
                        principalTable: "MaterialScoreRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscardItems_DiscardId_Material",
                table: "DiscardItems",
                columns: new[] { "DiscardId", "Material" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscardItems_MaterialScoreRuleId",
                table: "DiscardItems",
                column: "MaterialScoreRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Discards_CollectorPointId",
                table: "Discards",
                column: "CollectorPointId");

            migrationBuilder.CreateIndex(
                name: "IX_Discards_DepositorId",
                table: "Discards",
                column: "DepositorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscardItems");

            migrationBuilder.DropTable(
                name: "Discards");
        }
    }
}
