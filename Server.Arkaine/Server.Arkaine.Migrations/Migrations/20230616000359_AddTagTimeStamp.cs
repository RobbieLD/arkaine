using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Arkaine.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTagTimeStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Timestamp",
                table: "Tags",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "Tags");
        }
    }
}
