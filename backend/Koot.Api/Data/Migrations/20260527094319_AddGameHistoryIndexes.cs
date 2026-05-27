using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Koot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the composite indexes first so the leading-column FK
            // coverage stays satisfied across the swap; MariaDB refuses to
            // drop an index that's still backing a foreign key.
            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_HostUserId_Status_EndedAt",
                table: "game_sessions",
                columns: new[] { "HostUserId", "Status", "EndedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_game_answers_SessionId_QuestionId",
                table: "game_answers",
                columns: new[] { "SessionId", "QuestionId" });

            migrationBuilder.DropIndex(
                name: "IX_game_sessions_HostUserId",
                table: "game_sessions");

            migrationBuilder.DropIndex(
                name: "IX_game_answers_SessionId",
                table: "game_answers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the originals first, then drop the composites — same
            // reasoning as Up: keep FK coverage continuous.
            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_HostUserId",
                table: "game_sessions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_game_answers_SessionId",
                table: "game_answers",
                column: "SessionId");

            migrationBuilder.DropIndex(
                name: "IX_game_sessions_HostUserId_Status_EndedAt",
                table: "game_sessions");

            migrationBuilder.DropIndex(
                name: "IX_game_answers_SessionId_QuestionId",
                table: "game_answers");
        }
    }
}
