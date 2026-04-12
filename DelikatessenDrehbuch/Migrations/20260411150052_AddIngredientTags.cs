using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_peelable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_peelable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_cuttable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_cuttable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_grateable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_grateable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_fryable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_fryable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_roastable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_roastable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_grillable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_grillable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_steamable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_steamable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_boilable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_boilable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_searable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_searable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_poachable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_poachable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_smokable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_smokable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_flambeable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_flambeable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_blendable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_blendable] bit NOT NULL DEFAULT CAST(0 AS bit);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_peelable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_cuttable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_grateable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_fryable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_roastable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_grillable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_steamable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_boilable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_searable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_poachable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_smokable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_flambeable", table: "IngredientsAndNutrients");
            migrationBuilder.DropColumn(name: "is_blendable", table: "IngredientsAndNutrients");
        }
    }
}
