using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheGameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAIPlayerSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAI",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAI",
                table: "Users");
        }
    }
}
