using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElsaMina.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserForeignKeysToStatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill missing users for any pre-existing orphan stat rows so the foreign keys below apply cleanly.
            migrationBuilder.Sql(
                """
                INSERT INTO "Users" ("UserId", "UserName", "LastSeenAction")
                SELECT DISTINCT ids."UserId", ids."UserId", 0
                FROM (
                    SELECT "Id" AS "UserId" FROM "ArcadeLevels"
                    UNION SELECT "UserId" FROM "ConnectFourRatings"
                    UNION SELECT "UserId" FROM "FloodItScores"
                    UNION SELECT "UserId" FROM "LadderEloSnapshots"
                    UNION SELECT "UserId" FROM "LightsOutScores"
                    UNION SELECT "UserId" FROM "NameColors"
                    UNION SELECT "UserId" FROM "TarotStats"
                    UNION SELECT "UserId" FROM "TrackedEloUsers"
                    UNION SELECT "UserId" FROM "TwentyFortyEightScores"
                    UNION SELECT "Id" FROM "UserPoints"
                    UNION SELECT "UserId" FROM "VoltorbFlipLevels"
                    UNION SELECT "UserId" FROM "WordleScores"
                ) AS ids
                WHERE ids."UserId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."UserId" = ids."UserId");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedEloUsers_UserId",
                table: "TrackedEloUsers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArcadeLevels_Users_Id",
                table: "ArcadeLevels",
                column: "Id",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectFourRatings_Users_UserId",
                table: "ConnectFourRatings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloodItScores_Users_UserId",
                table: "FloodItScores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LadderEloSnapshots_Users_UserId",
                table: "LadderEloSnapshots",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LightsOutScores_Users_UserId",
                table: "LightsOutScores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NameColors_Users_UserId",
                table: "NameColors",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TarotStats_Users_UserId",
                table: "TarotStats",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedEloUsers_Users_UserId",
                table: "TrackedEloUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TwentyFortyEightScores_Users_UserId",
                table: "TwentyFortyEightScores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPoints_Users_Id",
                table: "UserPoints",
                column: "Id",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VoltorbFlipLevels_Users_UserId",
                table: "VoltorbFlipLevels",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WordleScores_Users_UserId",
                table: "WordleScores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArcadeLevels_Users_Id",
                table: "ArcadeLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_ConnectFourRatings_Users_UserId",
                table: "ConnectFourRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_FloodItScores_Users_UserId",
                table: "FloodItScores");

            migrationBuilder.DropForeignKey(
                name: "FK_LadderEloSnapshots_Users_UserId",
                table: "LadderEloSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_LightsOutScores_Users_UserId",
                table: "LightsOutScores");

            migrationBuilder.DropForeignKey(
                name: "FK_NameColors_Users_UserId",
                table: "NameColors");

            migrationBuilder.DropForeignKey(
                name: "FK_TarotStats_Users_UserId",
                table: "TarotStats");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedEloUsers_Users_UserId",
                table: "TrackedEloUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_TwentyFortyEightScores_Users_UserId",
                table: "TwentyFortyEightScores");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPoints_Users_Id",
                table: "UserPoints");

            migrationBuilder.DropForeignKey(
                name: "FK_VoltorbFlipLevels_Users_UserId",
                table: "VoltorbFlipLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_WordleScores_Users_UserId",
                table: "WordleScores");

            migrationBuilder.DropIndex(
                name: "IX_TrackedEloUsers_UserId",
                table: "TrackedEloUsers");
        }
    }
}
