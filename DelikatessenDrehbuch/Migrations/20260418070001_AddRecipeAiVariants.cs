using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeAiVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeAiVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseRecipeId = table.Column<int>(type: "int", nullable: false),
                    ParentVariantId = table.Column<int>(type: "int", nullable: true),
                    VariantType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedByUserHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AppliedChangeCount = table.Column<int>(type: "int", nullable: false),
                    LatestUserNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsSharedCanonical = table.Column<bool>(type: "bit", nullable: false),
                    StepPlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenderedStepsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenderedIngredientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenderedHighlightsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenderedTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RenderedSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAiVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAiVariants_RecipeAiVariants_ParentVariantId",
                        column: x => x.ParentVariantId,
                        principalTable: "RecipeAiVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipeAiVariants_RecipeBaseData_BaseRecipeId",
                        column: x => x.BaseRecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language",
                table: "RecipeAiVariants",
                columns: new[] { "BaseRecipeId", "CreatedByUserHash", "VariantType", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_IsSharedCanonical",
                table: "RecipeAiVariants",
                columns: new[] { "BaseRecipeId", "VariantType", "Language", "IsSharedCanonical" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAiVariants_ParentVariantId",
                table: "RecipeAiVariants",
                column: "ParentVariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeAiVariants");
        }
    }
}
