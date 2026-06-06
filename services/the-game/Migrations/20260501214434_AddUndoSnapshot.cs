using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheGameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddUndoSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UndoSnapshotJson",
                table: "GameStates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UndoSnapshotJson",
                table: "GameStates");
        }
    }
}
