using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElsaMina.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MakeDollCatalogueDriveBacked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DollHoldings_Dolls_DollId",
                table: "DollHoldings");

            migrationBuilder.DropTable(
                name: "Dolls");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dolls",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: true),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Size = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dolls", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_DollHoldings_Dolls_DollId",
                table: "DollHoldings",
                column: "DollId",
                principalTable: "Dolls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
