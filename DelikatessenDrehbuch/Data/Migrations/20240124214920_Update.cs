using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Data.Migrations
{
    public partial class Update : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recessions_Recipes_RecipeId",
                table: "Recessions");

            migrationBuilder.RenameColumn(
                name: "RecipeId",
                table: "Recessions",
                newName: "RecipesId");

            migrationBuilder.RenameIndex(
                name: "IX_Recessions_RecipeId",
                table: "Recessions",
                newName: "IX_Recessions_RecipesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recessions_Recipes_RecipesId",
                table: "Recessions",
                column: "RecipesId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recessions_Recipes_RecipesId",
                table: "Recessions");

            migrationBuilder.RenameColumn(
                name: "RecipesId",
                table: "Recessions",
                newName: "RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_Recessions_RecipesId",
                table: "Recessions",
                newName: "IX_Recessions_RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recessions_Recipes_RecipeId",
                table: "Recessions",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
