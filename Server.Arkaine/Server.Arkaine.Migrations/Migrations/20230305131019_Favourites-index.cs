using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Arkaine.Migrations.Migrations
{
    public partial class Favouritesindex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Favourites_UserName",
                table: "Favourites");

            migrationBuilder.CreateIndex(
                name: "IX_Favourites_Name",
                table: "Favourites",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Favourites_UserName",
                table: "Favourites",
                column: "UserName",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Favourites_Name",
                table: "Favourites");

            migrationBuilder.DropIndex(
                name: "IX_Favourites_UserName",
                table: "Favourites");

            migrationBuilder.CreateIndex(
                name: "IX_Favourites_UserName",
                table: "Favourites",
                column: "UserName");
        }
    }
}
