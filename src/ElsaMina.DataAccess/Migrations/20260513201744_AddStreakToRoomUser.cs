using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElsaMina.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakToRoomUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentStreak",
                table: "RoomUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastActivityDate",
                table: "RoomUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LongestStreak",
                table: "RoomUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStreak",
                table: "RoomUsers");

            migrationBuilder.DropColumn(
                name: "LastActivityDate",
                table: "RoomUsers");

            migrationBuilder.DropColumn(
                name: "LongestStreak",
                table: "RoomUsers");
        }
    }
}
