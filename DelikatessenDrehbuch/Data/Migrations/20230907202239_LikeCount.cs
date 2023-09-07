using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Data.Migrations
{
    public partial class LikeCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "Recipes",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LikeCount",
                table: "Recipes");
        }
    }
}
