using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Koot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDisconnectedToParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDisconnected",
                table: "game_participants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDisconnected",
                table: "game_participants");
        }
    }
}
