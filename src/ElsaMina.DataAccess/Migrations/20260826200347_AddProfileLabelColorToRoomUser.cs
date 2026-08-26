using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElsaMina.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileLabelColorToRoomUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileLabelColor",
                table: "RoomUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileLabelColor",
                table: "RoomUsers");
        }
    }
}
