using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcceptCount",
                table: "RecipeAiBaseRecipes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ServedCount",
                table: "RecipeAiBaseRecipes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptCount",
                table: "RecipeAiBaseRecipes");

            migrationBuilder.DropColumn(
                name: "ServedCount",
                table: "RecipeAiBaseRecipes");
        }
    }
}
