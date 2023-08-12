using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Data.Migrations
{
    public partial class handler : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipesHandlers_Ingredients_IngredientId",
                table: "RecipesHandlers");

            migrationBuilder.DropIndex(
                name: "IX_RecipesHandlers_IngredientId",
                table: "RecipesHandlers");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                table: "RecipesHandlers");

            migrationBuilder.AddColumn<int>(
                name: "IngredientHandlerId",
                table: "RecipesHandlers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipesHandlers_IngredientHandlerId",
                table: "RecipesHandlers",
                column: "IngredientHandlerId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipesHandlers_IngredientHandlers_IngredientHandlerId",
                table: "RecipesHandlers",
                column: "IngredientHandlerId",
                principalTable: "IngredientHandlers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipesHandlers_IngredientHandlers_IngredientHandlerId",
                table: "RecipesHandlers");

            migrationBuilder.DropIndex(
                name: "IX_RecipesHandlers_IngredientHandlerId",
                table: "RecipesHandlers");

            migrationBuilder.DropColumn(
                name: "IngredientHandlerId",
                table: "RecipesHandlers");

            migrationBuilder.AddColumn<int>(
                name: "IngredientId",
                table: "RecipesHandlers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RecipesHandlers_IngredientId",
                table: "RecipesHandlers",
                column: "IngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipesHandlers_Ingredients_IngredientId",
                table: "RecipesHandlers",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
