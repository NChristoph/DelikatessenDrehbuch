using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeAiBaseRecipesV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeAiBaseRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseRecipeId = table.Column<int>(type: "int", nullable: false),
                    ParentAiRecipeId = table.Column<int>(type: "int", nullable: true),
                    VariantType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    AiProvider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SelectedIngredientKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedByUserHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AppliedChangeCount = table.Column<int>(type: "int", nullable: false),
                    LatestUserNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsSharedCanonical = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PersonCount = table.Column<int>(type: "int", nullable: false),
                    PreparationTimeMinutes = table.Column<int>(type: "int", nullable: false),
                    PreparationText = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiBaseRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipes_RecipeAiBaseRecipes_ParentAiRecipeId",
                        column: x => x.ParentAiRecipeId,
                        principalTable: "RecipeAiBaseRecipes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipes_RecipeBaseData_BaseRecipeId",
                        column: x => x.BaseRecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeAiBaseRecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeAiBaseRecipeId = table.Column<int>(type: "int", nullable: false),
                    IngredientMeasureQuantityId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsModified = table.Column<bool>(type: "bit", nullable: false),
                    ChangeHint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiBaseRecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipeIngredients_IngredientMeasureQuantity_IngredientMeasureQuantityId",
                        column: x => x.IngredientMeasureQuantityId,
                        principalTable: "IngredientMeasureQuantity",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipeIngredients_RecipeAiBaseRecipes_RecipeAiBaseRecipeId",
                        column: x => x.RecipeAiBaseRecipeId,
                        principalTable: "RecipeAiBaseRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeAiBaseRecipeSelectedIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeAiBaseRecipeId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiBaseRecipeSelectedIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipeSelectedIngredients_IngredientsAndNutrients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "IngredientsAndNutrients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipeSelectedIngredients_RecipeAiBaseRecipes_RecipeAiBaseRecipeId",
                        column: x => x.RecipeAiBaseRecipeId,
                        principalTable: "RecipeAiBaseRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeAiBaseRecipeSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeAiBaseRecipeId = table.Column<int>(type: "int", nullable: false),
                    StepIndex = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiBaseRecipeSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiBaseRecipeSteps_RecipeAiBaseRecipes_RecipeAiBaseRecipeId",
                        column: x => x.RecipeAiBaseRecipeId,
                        principalTable: "RecipeAiBaseRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipeIngredients_IngredientMeasureQuantityId",
                table: "RecipeAiBaseRecipeIngredients",
                column: "IngredientMeasureQuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipeIngredients_RecipeAiBaseRecipeId_SortOrder",
                table: "RecipeAiBaseRecipeIngredients",
                columns: new[] { "RecipeAiBaseRecipeId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipes_BaseRecipeId_Language_VariantType_AiProvider_SelectedIngredientKey_CreatedByUserHash",
                table: "RecipeAiBaseRecipes",
                columns: new[] { "BaseRecipeId", "Language", "VariantType", "AiProvider", "SelectedIngredientKey", "CreatedByUserHash" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipes_BaseRecipeId_Language_VariantType_AiProvider_SelectedIngredientKey_IsSharedCanonical",
                table: "RecipeAiBaseRecipes",
                columns: new[] { "BaseRecipeId", "Language", "VariantType", "AiProvider", "SelectedIngredientKey", "IsSharedCanonical" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipes_ParentAiRecipeId",
                table: "RecipeAiBaseRecipes",
                column: "ParentAiRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipeSelectedIngredients_IngredientId",
                table: "RecipeAiBaseRecipeSelectedIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipeSelectedIngredients_RecipeAiBaseRecipeId_IngredientId",
                table: "RecipeAiBaseRecipeSelectedIngredients",
                columns: new[] { "RecipeAiBaseRecipeId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipeSelectedIngredients_RecipeAiBaseRecipeId_SortOrder",
                table: "RecipeAiBaseRecipeSelectedIngredients",
                columns: new[] { "RecipeAiBaseRecipeId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiBaseRecipeSteps_RecipeAiBaseRecipeId_StepIndex",
                table: "RecipeAiBaseRecipeSteps",
                columns: new[] { "RecipeAiBaseRecipeId", "StepIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeAiBaseRecipeIngredients");

            migrationBuilder.DropTable(
                name: "RecipeAiBaseRecipeSelectedIngredients");

            migrationBuilder.DropTable(
                name: "RecipeAiBaseRecipeSteps");

            migrationBuilder.DropTable(
                name: "RecipeAiBaseRecipes");
        }
    }
}
