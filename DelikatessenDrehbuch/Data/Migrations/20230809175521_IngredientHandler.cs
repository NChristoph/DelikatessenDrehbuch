using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Data.Migrations
{
    public partial class IngredientHandler : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Quantities_QuantityId",
                table: "Ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_Quantities_Metrics_MeasureId",
                table: "Quantities");

            migrationBuilder.DropIndex(
                name: "IX_Quantities_MeasureId",
                table: "Quantities");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_QuantityId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "MeasureId",
                table: "Quantities");

            migrationBuilder.DropColumn(
                name: "QuantityId",
                table: "Ingredients");

            migrationBuilder.CreateTable(
                name: "IngredientHandlers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    QuantityId = table.Column<int>(type: "int", nullable: false),
                    MeasureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientHandlers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientHandlers_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientHandlers_Metrics_MeasureId",
                        column: x => x.MeasureId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientHandlers_Quantities_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "Quantities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientHandlers_IngredientId",
                table: "IngredientHandlers",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientHandlers_MeasureId",
                table: "IngredientHandlers",
                column: "MeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientHandlers_QuantityId",
                table: "IngredientHandlers",
                column: "QuantityId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientHandlers");

            migrationBuilder.AddColumn<int>(
                name: "MeasureId",
                table: "Quantities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityId",
                table: "Ingredients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Quantities_MeasureId",
                table: "Quantities",
                column: "MeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_QuantityId",
                table: "Ingredients",
                column: "QuantityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Quantities_QuantityId",
                table: "Ingredients",
                column: "QuantityId",
                principalTable: "Quantities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quantities_Metrics_MeasureId",
                table: "Quantities",
                column: "MeasureId",
                principalTable: "Metrics",
                principalColumn: "Id");
        }
    }
}
