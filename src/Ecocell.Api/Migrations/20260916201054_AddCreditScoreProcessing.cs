using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditScoreProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditScoreRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditScoreRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditScoreRequests_Discards_DiscardId",
                        column: x => x.DiscardId,
                        principalTable: "Discards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepositorScoreTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<decimal>(type: "numeric(28,5)", precision: 28, scale: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositorScoreTransactions", x => x.Id);
                    table.CheckConstraint("CK_DepositorScoreTransactions_Points_Positive", "CAST(\"Points\" AS REAL) > 0");
                    table.ForeignKey(
                        name: "FK_DepositorScoreTransactions_Discards_DiscardId",
                        column: x => x.DiscardId,
                        principalTable: "Discards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepositorScoreTransactions_NaturalPeople_DepositorId",
                        column: x => x.DepositorId,
                        principalTable: "NaturalPeople",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepositorTotalScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalPoints = table.Column<decimal>(type: "numeric(28,5)", precision: 28, scale: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositorTotalScores", x => x.Id);
                    table.CheckConstraint("CK_DepositorTotalScores_TotalPoints_NonNegative", "CAST(\"TotalPoints\" AS REAL) >= 0");
                    table.ForeignKey(
                        name: "FK_DepositorTotalScores_NaturalPeople_DepositorId",
                        column: x => x.DepositorId,
                        principalTable: "NaturalPeople",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditScoreRequests_DiscardId",
                table: "CreditScoreRequests",
                column: "DiscardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditScoreRequests_DispatchedAt",
                table: "CreditScoreRequests",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DepositorScoreTransactions_DepositorId",
                table: "DepositorScoreTransactions",
                column: "DepositorId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositorScoreTransactions_DiscardId",
                table: "DepositorScoreTransactions",
                column: "DiscardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepositorTotalScores_DepositorId",
                table: "DepositorTotalScores",
                column: "DepositorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditScoreRequests");

            migrationBuilder.DropTable(
                name: "DepositorScoreTransactions");

            migrationBuilder.DropTable(
                name: "DepositorTotalScores");
        }
    }
}
