using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Services;

public class SaveNewRecipeServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly SaveNewRecipeService _service;

    public SaveNewRecipeServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqlite();
        _service = new SaveNewRecipeService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static IngredientsAndNutrients CreateTestNutrient(int id = 1, string name = "Mehl") => new()
    {
        Id = id,
        Name_DE = name, Name_EN = name, Name_PRT = name, Name_ESP = name,
        Name_ID = name, Name_NL = name, Name_SE = name, Name_DK = name,
        Name_NO = name, Name_MS = name,
        Genus_DE = "das", Genus_EN = "the", Genus_ESP = "el", Genus_PRT = "o",
        Genus_ID = "-", Genus_MS = "-", Genus_NL = "de", Genus_SE = "-",
        Genus_DK = "-", Genus_NO = "-"
    };

    private static Measure CreateTestMeasure(int id = 1, string unit = "g.") => new()
    {
        Id = id,
        Metrics_DE = unit, Metrics_EN = unit, Metrics_ESP = unit, Metrics_PRT = unit,
        Metrics_ID = unit, Metrics_MS = unit, Metrics_NL = unit, Metrics_SE = unit,
        Metrics_DK = unit, Metrics_NO = unit
    };

    private async Task SeedReferenceData()
    {
        _context.IngredientsAndNutrients.Add(CreateTestNutrient());
        _context.Metrics.Add(CreateTestMeasure());
        _context.Quantities.Add(new Quantity { Id = 1, Quantitys = 500 });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveNewAsync_NewRecipe_CreatesRecipeBaseData()
    {
        await SeedReferenceData();

        var model = new SaveNewRecipeModel
        {
            Querys = "test",
            Recipes = new Recipes
            {
                Name = "Testrezept",
                RecipePersonCount = 2,
                Category = "Hauptspeise",
                PreparationTime = 30
            },
            IngredientMeasureQuantity = new List<IngredientMeasureQuantity>
            {
                new()
                {
                    IngredientsAndNutrients = new IngredientsAndNutrients { Id = 1, Name_DE = "Mehl" },
                    Measure = new Measure { Metrics_DE = "g." },
                    Quantity = new Quantity { Quantitys = 500 }
                }
            },
            RecipeJoinPreparationSteps = new List<RecipeJoinPreparationSteps>()
        };

        await _service.SaveNewAsync(model, false);

        var recipe = await _context.RecipeBaseData.FirstOrDefaultAsync(r => r.Title == "Testrezept");
        recipe.Should().NotBeNull();
        recipe!.PersonCount.Should().Be(2);
        recipe.Category.Should().Be("Hauptspeise");
    }

    [Fact]
    public async Task SaveNewAsync_ExistingRecipe_UpdatesPersonCount()
    {
        await SeedReferenceData();

        _context.RecipeBaseData.Add(new RecipeBaseData
        {
            Title = "Bestehendes Rezept",
            PersonCount = 4,
            Category = "Vorspeise",
            Preferences = "test"
        });
        await _context.SaveChangesAsync();

        var model = new SaveNewRecipeModel
        {
            Querys = "test",
            Recipes = new Recipes
            {
                Name = "Bestehendes Rezept",
                RecipePersonCount = 6,
                Category = "Vorspeise",
                PreparationTime = 15
            },
            IngredientMeasureQuantity = new List<IngredientMeasureQuantity>
            {
                new()
                {
                    IngredientsAndNutrients = new IngredientsAndNutrients { Id = 1, Name_DE = "Mehl" },
                    Measure = new Measure { Metrics_DE = "g." },
                    Quantity = new Quantity { Quantitys = 500 }
                }
            },
            RecipeJoinPreparationSteps = new List<RecipeJoinPreparationSteps>()
        };

        await _service.SaveNewAsync(model, false);

        var recipe = await _context.RecipeBaseData.FirstAsync(r => r.Title == "Bestehendes Rezept");
        recipe.PersonCount.Should().Be(6);
    }

    [Fact]
    public async Task SaveNewAsync_InvalidIngredient_ThrowsNotFound()
    {
        var model = new SaveNewRecipeModel
        {
            Querys = "test",
            Recipes = new Recipes
            {
                Name = "Fehlrezept",
                RecipePersonCount = 2,
                Category = "Test",
                PreparationTime = 10
            },
            IngredientMeasureQuantity = new List<IngredientMeasureQuantity>
            {
                new()
                {
                    IngredientsAndNutrients = new IngredientsAndNutrients { Id = 999, Name_DE = "Nichtexistent" },
                    Measure = new Measure { Metrics_DE = "kg." },
                    Quantity = new Quantity { Quantitys = 1 }
                }
            },
            RecipeJoinPreparationSteps = new List<RecipeJoinPreparationSteps>()
        };

        var act = () => _service.SaveNewAsync(model, false);

        await act.Should().ThrowAsync<WorldMiniAppNotFoundException>();
    }

    [Fact]
    public async Task SaveNewAsync_WithPreparationSteps_CreatesSteps()
    {
        await SeedReferenceData();

        var model = new SaveNewRecipeModel
        {
            Querys = "test",
            Recipes = new Recipes
            {
                Name = "Rezept mit Schritten",
                RecipePersonCount = 2,
                Category = "Hauptspeise",
                PreparationTime = 45
            },
            IngredientMeasureQuantity = new List<IngredientMeasureQuantity>
            {
                new()
                {
                    IngredientsAndNutrients = new IngredientsAndNutrients { Id = 1, Name_DE = "Mehl" },
                    Measure = new Measure { Metrics_DE = "g." },
                    Quantity = new Quantity { Quantitys = 500 }
                }
            },
            RecipeJoinPreparationSteps = new List<RecipeJoinPreparationSteps>
            {
                new()
                {
                    RecipePreparationStep = new RecipePreparationSteps
                    {
                        Step_DE = "Mehl sieben",
                        Step_EN = "Sift flour"
                    }
                }
            }
        };

        await _service.SaveNewAsync(model, false);

        var steps = await _context.RecipePreparationSteps.ToListAsync();
        steps.Should().Contain(s => s.Step_DE == "Mehl sieben");
    }

    [Fact]
    public async Task SaveNewAsync_WithImage_CreatesImageRecord()
    {
        await SeedReferenceData();

        var model = new SaveNewRecipeModel
        {
            Querys = "test",
            Recipes = new Recipes
            {
                Name = "Rezept mit Bild",
                RecipePersonCount = 2,
                Category = "Dessert",
                PreparationTime = 20,
                ImagePath = "https://example.com/image.webp"
            },
            IngredientMeasureQuantity = new List<IngredientMeasureQuantity>
            {
                new()
                {
                    IngredientsAndNutrients = new IngredientsAndNutrients { Id = 1, Name_DE = "Mehl" },
                    Measure = new Measure { Metrics_DE = "g." },
                    Quantity = new Quantity { Quantitys = 500 }
                }
            },
            RecipeJoinPreparationSteps = new List<RecipeJoinPreparationSteps>()
        };

        await _service.SaveNewAsync(model, true);

        var image = await _context.RecipeBaseDataImage.FirstOrDefaultAsync();
        image.Should().NotBeNull();
        image!.Image.Should().Be("https://example.com/image.webp");
        image.WorldAppImage.Should().BeTrue();
    }
}
