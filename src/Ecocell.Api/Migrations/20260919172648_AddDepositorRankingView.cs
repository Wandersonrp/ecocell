using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecocell.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositorRankingView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Addresses_State_City_Normalized"
                ON "Addresses" (
                    upper(btrim("State")),
                    lower(btrim("City"))
                );
                """);

            migrationBuilder.Sql("""
                CREATE VIEW v_ranking_depositor AS
                WITH national_totals AS (
                    SELECT
                        'National'::text AS "Scope",
                        NULL::text AS "State",
                        NULL::text AS "City",
                        totals."DepositorId" AS "DepositorId",
                        natural_people."FullName" AS "FullName",
                        totals."TotalPoints"::numeric(28, 5) AS "TotalPoints"
                    FROM "DepositorTotalScores" AS totals
                    INNER JOIN "NaturalPeople" AS natural_people
                        ON natural_people."PersonId" = totals."DepositorId"
                    INNER JOIN "People" AS people
                        ON people."PersonId" = totals."DepositorId"
                    WHERE people."PersonStatus" = 1
                      AND totals."TotalPoints" > 0
                ),
                national_ranked AS (
                    SELECT
                        "Scope",
                        "State",
                        "City",
                        "DepositorId",
                        "FullName",
                        "TotalPoints",
                        dense_rank() OVER (
                            ORDER BY "TotalPoints" DESC
                        ) AS "Position"
                    FROM national_totals
                ),
                municipal_totals AS (
                    SELECT
                        'Municipal'::text AS "Scope",
                        upper(btrim(addresses."State")) AS "State",
                        lower(btrim(addresses."City")) AS "City",
                        transactions."DepositorId" AS "DepositorId",
                        natural_people."FullName" AS "FullName",
                        sum(transactions."Points")::numeric(28, 5) AS "TotalPoints"
                    FROM "DepositorScoreTransactions" AS transactions
                    INNER JOIN "Discards" AS discards
                        ON discards."Id" = transactions."DiscardId"
                    INNER JOIN "LegalPeople" AS collector_points
                        ON collector_points."PersonId" = discards."CollectorPointId"
                    INNER JOIN "Addresses" AS addresses
                        ON addresses."Id" = collector_points."AddressId"
                    INNER JOIN "NaturalPeople" AS natural_people
                        ON natural_people."PersonId" = transactions."DepositorId"
                    INNER JOIN "People" AS people
                        ON people."PersonId" = transactions."DepositorId"
                    WHERE people."PersonStatus" = 1
                      AND nullif(btrim(addresses."City"), '') IS NOT NULL
                      AND nullif(btrim(addresses."State"), '') IS NOT NULL
                    GROUP BY
                        upper(btrim(addresses."State")),
                        lower(btrim(addresses."City")),
                        transactions."DepositorId",
                        natural_people."FullName"
                    HAVING sum(transactions."Points") > 0
                ),
                municipal_ranked AS (
                    SELECT
                        "Scope",
                        "State",
                        "City",
                        "DepositorId",
                        "FullName",
                        "TotalPoints",
                        dense_rank() OVER (
                            PARTITION BY "State", "City"
                            ORDER BY "TotalPoints" DESC
                        ) AS "Position"
                    FROM municipal_totals
                )
                SELECT
                    "Scope", "State", "City", "DepositorId",
                    "FullName", "TotalPoints", "Position"
                FROM national_ranked
                UNION ALL
                SELECT
                    "Scope", "State", "City", "DepositorId",
                    "FullName", "TotalPoints", "Position"
                FROM municipal_ranked;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_ranking_depositor;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Addresses_State_City_Normalized\";");
        }
    }
}
