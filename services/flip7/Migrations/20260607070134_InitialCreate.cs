using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flip7Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetScore = table.Column<int>(type: "integer", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    DealerSeat = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WinnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoundStateJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Seat = table.Column<int>(type: "integer", nullable: false),
                    IsAi = table.Column<bool>(type: "boolean", nullable: false),
                    AiStyle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AiDifficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CumulativeScore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundScore = table.Column<int>(type: "integer", nullable: false),
                    Busted = table.Column<bool>(type: "boolean", nullable: false),
                    AchievedFlip7 = table.Column<bool>(type: "boolean", nullable: false),
                    CumulativeAfter = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundResults_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status",
                table: "GameSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GameSessionId_Seat",
                table: "Players",
                columns: new[] { "GameSessionId", "Seat" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_UserId",
                table: "Players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundResults_GameSessionId_RoundNumber",
                table: "RoundResults",
                columns: new[] { "GameSessionId", "RoundNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "RoundResults");

            migrationBuilder.DropTable(
                name: "GameSessions");
        }
    }
}
