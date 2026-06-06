using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheGameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMoveHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MoveHistory",
                table: "GameStates",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoveHistory",
                table: "GameStates");
        }
    }
}
