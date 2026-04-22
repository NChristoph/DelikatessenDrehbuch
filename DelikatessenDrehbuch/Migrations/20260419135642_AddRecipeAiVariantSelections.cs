using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeAiVariantSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FoodCategoryId",
                table: "IngredientsAndNutrients",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "food_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category_Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name_DE = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_EN = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_PRT = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_ESP = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_ID = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_NL = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_SE = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_DK = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_NO = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name_MS = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientsAndNutrients_FoodCategoryId",
                table: "IngredientsAndNutrients",
                column: "FoodCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_food_categories_Category_Key",
                table: "food_categories",
                column: "Category_Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_FoodCategories",
                table: "IngredientsAndNutrients",
                column: "FoodCategoryId",
                principalTable: "food_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_FoodCategories",
                table: "IngredientsAndNutrients");

            migrationBuilder.DropTable(
                name: "food_categories");

            migrationBuilder.DropIndex(
                name: "IX_IngredientsAndNutrients_FoodCategoryId",
                table: "IngredientsAndNutrients");

            migrationBuilder.DropColumn(
                name: "FoodCategoryId",
                table: "IngredientsAndNutrients");
        }
    }
}
