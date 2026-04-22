using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientMeasureQuantityLinkToAiVariantRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally scoped: only AI-variant schema changes.
            // Other unrelated schema diffs were removed to keep rollout safe.

            migrationBuilder.AddColumn<string>(
                name: "AiProvider",
                table: "RecipeAiVariants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "openai");

            migrationBuilder.AddColumn<string>(
                name: "IngredientsText",
                table: "RecipeAiVariants",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreparationText",
                table: "RecipeAiVariants",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SelectedIngredientKey",
                table: "RecipeAiVariants",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            // NOTE: Identity column-size normalization is intentionally not done in this migration.

            migrationBuilder.CreateTable(
                name: "RecipeAiVariantIngredientRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeAiVariantId = table.Column<int>(type: "int", nullable: false),
                    IngredientMeasureQuantityId = table.Column<int>(type: "int", nullable: true),
                    IngredientId = table.Column<int>(type: "int", nullable: true),
                    MeasureId = table.Column<int>(type: "int", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    QuantityText = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiVariantIngredientRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiVariantIngredientRows_IngredientMeasureQuantity_IngredientMeasureQuantityId",
                        column: x => x.IngredientMeasureQuantityId,
                        principalTable: "IngredientMeasureQuantity",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiVariantIngredientRows_IngredientsAndNutrients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "IngredientsAndNutrients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiVariantIngredientRows_Metrics_MeasureId",
                        column: x => x.MeasureId,
                        principalTable: "Metrics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiVariantIngredientRows_RecipeAiVariants_RecipeAiVariantId",
                        column: x => x.RecipeAiVariantId,
                        principalTable: "RecipeAiVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeAiVariantSelectedIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeAiVariantId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiVariantSelectedIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiVariantSelectedIngredients_IngredientsAndNutrients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "IngredientsAndNutrients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiVariantSelectedIngredients_RecipeAiVariants_RecipeAiVariantId",
                        column: x => x.RecipeAiVariantId,
                        principalTable: "RecipeAiVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language_AiProvider_SelectedIngredientKey",
                table: "RecipeAiVariants",
                columns: new[] { "BaseRecipeId", "CreatedByUserHash", "VariantType", "Language", "AiProvider", "SelectedIngredientKey" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_AiProvider_SelectedIngredientKey_IsSharedCanonical",
                table: "RecipeAiVariants",
                columns: new[] { "BaseRecipeId", "VariantType", "Language", "AiProvider", "SelectedIngredientKey", "IsSharedCanonical" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantIngredientRows_IngredientId",
                table: "RecipeAiVariantIngredientRows",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantIngredientRows_IngredientMeasureQuantityId",
                table: "RecipeAiVariantIngredientRows",
                column: "IngredientMeasureQuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantIngredientRows_MeasureId",
                table: "RecipeAiVariantIngredientRows",
                column: "MeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantIngredientRows_RecipeAiVariantId_SortOrder",
                table: "RecipeAiVariantIngredientRows",
                columns: new[] { "RecipeAiVariantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantSelectedIngredients_IngredientId",
                table: "RecipeAiVariantSelectedIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantSelectedIngredients_RecipeAiVariantId_IngredientId",
                table: "RecipeAiVariantSelectedIngredients",
                columns: new[] { "RecipeAiVariantId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariantSelectedIngredients_RecipeAiVariantId_SortOrder",
                table: "RecipeAiVariantSelectedIngredients",
                columns: new[] { "RecipeAiVariantId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeAiVariantIngredientRows");

            migrationBuilder.DropTable(
                name: "RecipeAiVariantSelectedIngredients");

            migrationBuilder.DropIndex(
                name: "IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language_AiProvider_SelectedIngredientKey",
                table: "RecipeAiVariants");

            migrationBuilder.DropIndex(
                name: "IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_AiProvider_SelectedIngredientKey_IsSharedCanonical",
                table: "RecipeAiVariants");

            migrationBuilder.DropColumn(
                name: "AiProvider",
                table: "RecipeAiVariants");

            migrationBuilder.DropColumn(
                name: "IngredientsText",
                table: "RecipeAiVariants");

            migrationBuilder.DropColumn(
                name: "PreparationText",
                table: "RecipeAiVariants");

            migrationBuilder.DropColumn(
                name: "SelectedIngredientKey",
                table: "RecipeAiVariants");
            // No rollback for unrelated schema changes (see Up()).
        }
    }
}
