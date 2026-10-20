using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElsaMina.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionGrantsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionGrants",
                columns: table => new
                {
                    RoomId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    GrantedBy = table.Column<string>(type: "text", nullable: true),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionGrants", x => new { x.RoomId, x.UserId, x.Kind, x.Target });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionGrants");
        }
    }
}
