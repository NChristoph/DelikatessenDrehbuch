using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Data.Migrations
{
    public partial class Recipetype : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipesId = table.Column<int>(type: "int", nullable: false),
                    Vegan = table.Column<bool>(type: "bit", nullable: false),
                    Vegetarian = table.Column<bool>(type: "bit", nullable: false),
                    LowCarb = table.Column<bool>(type: "bit", nullable: false),
                    BBQ = table.Column<bool>(type: "bit", nullable: false),
                    Pastry = table.Column<bool>(type: "bit", nullable: false),
                    Bread = table.Column<bool>(type: "bit", nullable: false),
                    Cake = table.Column<bool>(type: "bit", nullable: false),
                    Biscuits = table.Column<bool>(type: "bit", nullable: false),
                    Cocktails = table.Column<bool>(type: "bit", nullable: false),
                    Pie = table.Column<bool>(type: "bit", nullable: false),
                    Diet = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeTypes_Recipes_RecipesIdId",
                        column: x => x.RecipesId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeTypes_RecipesIdId",
                table: "RecipeTypes",
                column: "RecipesIdId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeTypes");
        }
    }
}
