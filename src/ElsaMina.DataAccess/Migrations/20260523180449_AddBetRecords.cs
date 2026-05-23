using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElsaMina.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBetRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BetRecords",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoomId = table.Column<string>(type: "text", nullable: false),
                    CorrectBetsCount = table.Column<int>(type: "integer", nullable: false),
                    TotalBetsCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetRecords", x => new { x.UserId, x.RoomId });
                    table.ForeignKey(
                        name: "FK_BetRecords_RoomUsers_UserId_RoomId",
                        columns: x => new { x.UserId, x.RoomId },
                        principalTable: "RoomUsers",
                        principalColumns: new[] { "Id", "RoomId" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BetRecords");
        }
    }
}
