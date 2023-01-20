using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Arkaine.Migrations.Migrations
{
    public partial class RemoveBucket : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ratings_Bucket",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "Bucket",
                table: "Ratings");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_FileName",
                table: "Ratings",
                column: "FileName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ratings_FileName",
                table: "Ratings");

            migrationBuilder.AddColumn<string>(
                name: "Bucket",
                table: "Ratings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_Bucket",
                table: "Ratings",
                column: "Bucket",
                unique: true);
        }
    }
}
