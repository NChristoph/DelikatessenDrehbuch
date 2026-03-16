using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260316104500_AddIconToIngredientsAndNutrients")]
    public partial class AddIconToIngredientsAndNutrients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "IngredientsAndNutrients",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Icon",
                table: "IngredientsAndNutrients");
        }
    }
}
