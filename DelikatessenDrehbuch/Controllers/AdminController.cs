using Azure.Storage.Blobs;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.MyExceptions;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using System.Threading.Tasks;






namespace DelikatessenDrehbuch.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {


        private readonly AddRecipeException _myExceptions;
        private readonly ApplicationDbContext _context;
        private readonly IRecipesService _recipesService;
        private readonly IAdminControllerModelService _adminControllerModelService;
        private readonly IBlobAzureService _blobAzureService;
        private readonly IIngredientService _ingredientService;
        private readonly IQueryService _queryService;
        private readonly IRecipesHandlerService _recipesHandlerService;
        private readonly IMeasureService _measureService;
        private readonly IQuantityService _quantityService;
        private readonly IMealPlanService _mealPlanService;
        private readonly INutrientService _nutrientService;
        private readonly ISupportTicketService _supportTicketService;
        private readonly ISaveNewRecipeService _saveNewRecipeService;
        private readonly IBlobUploadService _blobUploadService;


        public AdminController(ApplicationDbContext context, AddRecipeException myExceptions,
                               IRecipesService recipesService, IAdminControllerModelService adminControllerModelService,
                               IBlobAzureService blobAzureService, IIngredientService ingredientService,
                               IQueryService queryService, IRecipesHandlerService recipesHandlerService,
                               IMeasureService measureService, IQuantityService quantityService,
                               IMealPlanService mealPlanService, INutrientService nutrientService,
                               ISupportTicketService supportTicketService, ISaveNewRecipeService saveNewRecipeService,
                               IBlobUploadService blobUploadService)
        {


            _myExceptions = myExceptions;
            _recipesService = recipesService;
            _adminControllerModelService = adminControllerModelService;
            _blobAzureService = blobAzureService;
            _ingredientService = ingredientService;
            _queryService = queryService;
            _recipesHandlerService = recipesHandlerService;
            _measureService = measureService;
            _quantityService = quantityService;
            _mealPlanService = mealPlanService;
            _nutrientService = nutrientService;
            _supportTicketService = supportTicketService;
            _saveNewRecipeService = saveNewRecipeService;
            _blobUploadService = blobUploadService;
            _context = context;


        }

        public IActionResult Index()
        {
            var model = _adminControllerModelService.GetAdminControlerModel();

            return View(model);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportRecipeJsonSchemaAsync()
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Preparation,
                    x.Category
                })
                .ToListAsync();

            var recipeIngredients = await _context.RecipesHandlers
                .AsNoTracking()
                .Where(x => x.IngredientHandler != null)
                .Select(x => new
                {
                    RecipeId = x.Recipe.Id,
                    IngredientId = x.IngredientHandler.Ingredient.Id,
                    IngredientName = x.IngredientHandler.Ingredient.Name,
                    Quantity = x.IngredientHandler.Quantity.Quantitys,
                    Unit = x.IngredientHandler.Measure.UnitOfMeasurement
                })
                .ToListAsync();

            var ingredientsByRecipe = recipeIngredients
                .GroupBy(x => x.RecipeId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(x => new RecipeIngredientExportModel
                        {
                            IngredientId = x.IngredientId,
                            IngredientName = x.IngredientName,
                            Quantity = x.Quantity,
                            Unit = x.Unit
                        })
                        .ToList());

            var exportRecipes = recipes
                .Select(recipe => new RecipeJsonExportModel
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Preparation = recipe.Preparation,
                    Category = recipe.Category,
                    CategoryShort = MapCategoryToCourse(recipe.Category),
                    Ingredients = ingredientsByRecipe.TryGetValue(recipe.Id, out var ingredients)
                        ? ingredients
                        : new List<RecipeIngredientExportModel>()
                })
                .ToList();

            var preparationSteps = await _context.RecipePreperationSteps
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new PreparationStepExportModel
                {
                    Id = x.Id,
                    StepDe = x.Step_DE,
                    StepEn = x.Step_EN,
                    StepPrt = x.Step_PRT,
                    StepEsp = x.Step_ESP,
                    Phase = x.Phase,
                    Equipment = x.Equipment
                })
                .ToListAsync();

            var ingredientsAndNutrients = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .OrderBy(x => x.Id)
                .Select(x => new IngredientNutrientExportModel
                {
                    Id = x.Id,
                    NameDe = x.Name_DE,
                    NameEn = x.Name_EN,
                    NamePrt = x.Name_PRT,
                    NameEsp = x.Name_ESP,
                    GroupId = x.Group != null ? x.Group.Id : (int?)null,
                    GroupName = x.Group != null ? x.Group.Name : null,
                    Calories_a_100g = x.Calories_a_100g,
                    Weight_per_piece = x.Weight_per_piece,
                    Fat_a_100g = x.Fat_a_100g,
                    Saturated_fat_a_100g = x.Saturated_fat_a_100g,
                    Carbohydrates_a_100g = x.Carbohydrates_a_100g,
                    Sugar_a_100g = x.Sugar_a_100g,
                    Salt_a_100g = x.Salt_a_100g,
                    Protein_a_100g = x.Protein_a_100g,
                    Fiber_a_100g = x.Fiber_a_100g
                })
                .ToListAsync();

            // Namens-Lookup: Name_DE (normalisiert) → IngredientsAndNutrients-ID
            var newIngLookup = ingredientsAndNutrients
                .GroupBy(x => x.NameDe.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);

            // Alle eindeutigen alten Zutaten aus RecipesHandlers
            var allOldIngredients = recipeIngredients
                .GroupBy(x => x.IngredientId)
                .Select(g => new { OldId = g.Key, Name = g.First().IngredientName })
                .ToList();

            // Mapping: Alte Ingredient-ID → IngredientsAndNutrients-ID (wenn Name übereinstimmt)
            var legacyIngredientIdMap = allOldIngredients
                .Select(x => new LegacyIngredientMapModel
                {
                    OldId = x.OldId,
                    Name = x.Name,
                    NewId = newIngLookup.TryGetValue(x.Name.Trim().ToLowerInvariant(), out var newId) ? newId : (int?)null
                })
                .OrderBy(x => x.Name)
                .ToList();

            // Zutaten ohne Treffer im neuen System – für fehlende_zutaten.txt
            var missingIngredients = legacyIngredientIdMap
                .Where(x => x.NewId == null)
                .OrderByDescending(x =>
                    recipeIngredients.Count(r => r.IngredientId == x.OldId))
                .ToList();

            var schema = new
            {
                schema_version = "1.1",
                description = "Exportstruktur für Rezepte inkl. Zutaten-Mengen aus Recipes + RecipesHandlers sowie IngredientsAndNutrients (neues Nährwert-System).",
                root_fields = new[]
                {
                    "exported_at_utc",
                    "record_count",
                    "recipes",
                    "preparation_steps",
                    "ingredients_and_nutrients"
                },
                recipe_fields = new[]
                {
                    "id",
                    "name",
                    "preparation",
                    "category",
                    "category_short (vor|haupt|nach)",
                    "ingredients[]"
                },
                ingredient_fields = new[]
                {
                    "ingredient_id",
                    "ingredient_name",
                    "quantity",
                    "unit"
                },
                preparation_step_fields = new[]
                {
                    "id",
                    "step_de",
                    "step_en",
                    "step_prt",
                    "step_esp",
                    "phase",
                    "equipment"
                },
                ingredient_nutrient_fields = new[]
                {
                    "id",
                    "name_de",
                    "name_en",
                    "name_prt",
                    "name_esp",
                    "group_id",
                    "group_name",
                    "calories_a_100g",
                    "weight_per_piece",
                    "fat_a_100g",
                    "saturated_fat_a_100g",
                    "carbohydrates_a_100g",
                    "sugar_a_100g",
                    "salt_a_100g",
                    "protein_a_100g",
                    "fiber_a_100g"
                },
                legacy_ingredient_map_fields = new[]
                {
                    "old_id   (Ingredient.Id aus RecipesHandlers)",
                    "name     (Zutatenname wie in Rezepten)",
                    "new_id   (IngredientsAndNutrients.Id, null = noch nicht im neuen System)"
                }
            };

            var payload = new
            {
                exported_at_utc = DateTime.UtcNow,
                record_count = exportRecipes.Count,
                missing_ingredient_count = missingIngredients.Count,
                schema,
                recipes = exportRecipes,
                preparation_steps = preparationSteps,
                ingredients_and_nutrients = ingredientsAndNutrients,
                legacy_ingredient_id_map = legacyIngredientIdMap
            };

            var exportDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data", "exports");
            Directory.CreateDirectory(exportDirectory);

            var fileName = $"recipe_schema_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(exportDirectory, fileName);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(filePath, json);

            // Gleichzeitig das Step-Mapping aktualisieren
            var mappingStats = await BuildAndSaveStepMappingAsync();

            TempData["ExportJsonMessage"] = $"JSON Export erstellt: data/exports/{fileName} | Zutaten (neu): {ingredientsAndNutrients.Count} | Ohne Zuordnung: {missingIngredients.Count} | Step-Mapping: {mappingStats}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateStepMappingAsync()
        {
            var stats = await BuildAndSaveStepMappingAsync();
            TempData["ExportJsonMessage"] = $"Step-Mapping aktualisiert: {stats}";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> BuildAndSaveStepMappingAsync()
        {
            // ── 1. Daten aus DB laden ─────────────────────────────────────────
            var ingredients = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var allSteps = await _context.RecipePreperationSteps
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            // ingredient_to_steps: kommt aus JoinIngredientPreperationStep (DB)
            var joins = await _context.JoinIngredientPreperationStep
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Include(x => x.Preperation)
                .ToListAsync();

            var ingredientToSteps = joins
                .GroupBy(j => j.Ingredient.Id)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.Select(j => j.Preperation.Id).Distinct().OrderBy(id => id).ToList()
                );

            // Rezept-Zutaten-Beziehungen für Häufigkeit + Combos + Templates
            var recipeIngredientLinks = await _context.RecipeJoinIngredientMeasureQuantity
                .AsNoTracking()
                .Include(x => x.Recipe)
                .Include(x => x.Ingredient).ThenInclude(i => i.IngredientsAndNutrients)
                .Where(x => x.Ingredient.IngredientsAndNutrients != null)
                .Select(x => new
                {
                    RecipeId       = x.Recipe.Id,
                    RecipeTitle    = x.Recipe.Title,
                    RecipeCategory = x.Recipe.Category,
                    IngredientId   = x.Ingredient.IngredientsAndNutrients.Id
                })
                .ToListAsync();

            // ── 2. ingredient_catalog ─────────────────────────────────────────
            var recipeCountPerIngredient = recipeIngredientLinks
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RecipeId).Distinct().Count());

            var ingredientCatalog = ingredients.ToDictionary(
                ing => ing.Id.ToString(),
                ing => (object)new
                {
                    id           = ing.Id,
                    name         = ing.Name_DE,
                    name_en      = ing.Name_EN,
                    group        = ing.Group?.Name ?? string.Empty,
                    recipe_count = recipeCountPerIngredient.GetValueOrDefault(ing.Id, 0)
                }
            );

            // ── 3. ingredient_combos (Top 80 Co-Occurrenzen) ──────────────────
            var ingredientsByRecipe = recipeIngredientLinks
                .GroupBy(x => x.RecipeId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.IngredientId).Distinct().ToList());

            var coOccurrence = new Dictionary<(int, int), int>();
            foreach (var (_, ids) in ingredientsByRecipe)
            {
                var sorted = ids.OrderBy(x => x).ToList();
                for (int i = 0; i < sorted.Count; i++)
                    for (int j = i + 1; j < sorted.Count; j++)
                    {
                        var key = (sorted[i], sorted[j]);
                        coOccurrence[key] = coOccurrence.GetValueOrDefault(key, 0) + 1;
                    }
            }

            var ingredientComboList = coOccurrence
                .OrderByDescending(kv => kv.Value)
                .Take(80)
                .Select(kv => new
                {
                    ingredients    = new[] { kv.Key.Item1, kv.Key.Item2 },
                    names          = new[]
                    {
                        ingredients.FirstOrDefault(i => i.Id == kv.Key.Item1)?.Name_DE ?? string.Empty,
                        ingredients.FirstOrDefault(i => i.Id == kv.Key.Item2)?.Name_DE ?? string.Empty
                    },
                    co_occurrence  = kv.Value,
                    shared_steps   = ingredientToSteps.GetValueOrDefault(kv.Key.Item1.ToString(), new())
                                         .Intersect(ingredientToSteps.GetValueOrDefault(kv.Key.Item2.ToString(), new()))
                                         .OrderBy(x => x).ToList(),
                    combined_steps = ingredientToSteps.GetValueOrDefault(kv.Key.Item1.ToString(), new())
                                         .Union(ingredientToSteps.GetValueOrDefault(kv.Key.Item2.ToString(), new()))
                                         .OrderBy(x => x).ToList()
                })
                .ToList();

            // ── 4. recipe_type_templates (TF-IDF-Signature pro Kategorie-Pattern) ─
            var typePatterns = new[]
            {
                "lasagne","suppe","eintopf","salat","pasta","curry","pizza","burger",
                "risotto","stir-fry","taco","bowl","braten","chili","steak",
                "omelette","frittata","wrap","sandwich","smoothie","wok","pfannkuchen",
                "quiche","tartar"
            };

            int totalRecipes = ingredientsByRecipe.Count;
            var recipeTitles = recipeIngredientLinks
                .GroupBy(x => x.RecipeId)
                .ToDictionary(g => g.Key, g => g.First().RecipeTitle?.ToLowerInvariant() ?? string.Empty);

            var recipeTypeTemplates = new Dictionary<string, object>();
            foreach (var pattern in typePatterns)
            {
                var matchingRecipeIds = recipeTitles
                    .Where(kv => kv.Value.Contains(pattern))
                    .Select(kv => kv.Key)
                    .ToHashSet();

                if (matchingRecipeIds.Count < 3) continue;

                // TF: Häufigkeit in passenden Rezepten
                var tfByIngredient = recipeIngredientLinks
                    .Where(x => matchingRecipeIds.Contains(x.RecipeId))
                    .GroupBy(x => x.IngredientId)
                    .ToDictionary(g => g.Key, g => (double)g.Select(x => x.RecipeId).Distinct().Count() / matchingRecipeIds.Count);

                // IDF: Seltenheit gesamt
                var signatures = tfByIngredient
                    .Where(kv => kv.Value >= 0.1)
                    .Select(kv =>
                    {
                        int globalCount = recipeCountPerIngredient.GetValueOrDefault(kv.Key, 1);
                        double idf = Math.Log((double)totalRecipes / globalCount);
                        double specificity = Math.Round(kv.Value * idf, 2);
                        var ing = ingredients.FirstOrDefault(i => i.Id == kv.Key);
                        return new
                        {
                            ingredient_id   = kv.Key,
                            ingredient_name = ing?.Name_DE ?? string.Empty,
                            specificity,
                            frequency       = Math.Round(kv.Value, 2)
                        };
                    })
                    .Where(x => x.specificity > 0.5)
                    .OrderByDescending(x => x.specificity)
                    .Take(10)
                    .ToList();

                if (!signatures.Any()) continue;

                recipeTypeTemplates[pattern] = new
                {
                    name_pattern           = pattern,
                    recipe_count           = matchingRecipeIds.Count,
                    signature_ingredients  = signatures
                };
            }

            // ── 5. preparation_steps aus DB ───────────────────────────────────
            var preparationSteps = allSteps.Select(s => new
            {
                Id        = s.Id,
                StepDe    = s.Step_DE,
                StepEn    = s.Step_EN,
                StepPrt   = s.Step_PRT,
                StepEsp   = s.Step_ESP,
                Phase     = s.Phase,
                Equipment = s.Equipment
            }).ToList();

            // ── 6. Mapping zusammenbauen + speichern ──────────────────────────
            var phases    = new Dictionary<string, string> { {"0","Basis"},{"1","Vorbereitung"},{"2","Kochen"},{"3","Würzen"},{"4","Finish"} };
            var equipment = new Dictionary<string, string> { {"0","Keins"},{"1","Backofen"},{"2","Pfanne"},{"3","Topf"},{"4","Bräter"},{"5","Kochfeld"} };

            var mapping = new
            {
                _meta = new
                {
                    version            = "2.1",
                    generated_at       = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ingredient_system  = $"IngredientsAndNutrients – {ingredients.Count} Einträge",
                    description        = "Mapping von Zutaten zu Zubereitungsschritten + Rezepttyp-Erkennung für Recipe Step Suggestion Engine.",
                    usage              = "Fetch via /data/recipe_step_mapping.json | ingredient_to_steps kommt aus JoinIngredientPreperationStep (DB-Tabelle)"
                },
                phases,
                equipment,
                preparation_steps     = preparationSteps,
                ingredient_to_steps   = ingredientToSteps,
                recipe_type_templates = recipeTypeTemplates,
                ingredient_combos     = ingredientComboList,
                ingredient_catalog    = ingredientCatalog
            };

            var mappingJson = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
            var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "recipe_step_mapping.json");
            Directory.CreateDirectory(Path.GetDirectoryName(wwwrootPath)!);
            await System.IO.File.WriteAllTextAsync(wwwrootPath, mappingJson);

            return $"{ingredients.Count} Zutaten | {ingredientToSteps.Count} Ingredient-Step-Mappings | {recipeTypeTemplates.Count} Templates | {ingredientComboList.Count} Combos";
        }

        private static string MapCategoryToCourse(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "haupt";

            var normalized = category.Trim().ToLower();
            if (normalized.Contains("vor"))
                return "vor";
            if (normalized.Contains("nach") || normalized.Contains("dessert"))
                return "nach";

            return "haupt";
        }

        private sealed class RecipeJsonExportModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Preparation { get; set; } = string.Empty;
            public string? Category { get; set; }
            public string CategoryShort { get; set; } = string.Empty;
            public List<RecipeIngredientExportModel> Ingredients { get; set; } = new();
        }

        private sealed class RecipeIngredientExportModel
        {
            public int IngredientId { get; set; }
            public string IngredientName { get; set; } = string.Empty;
            public double Quantity { get; set; }
            public string Unit { get; set; } = string.Empty;
        }

        private sealed class PreparationStepExportModel
        {
            public int Id { get; set; }
            public string StepDe { get; set; } = string.Empty;
            public string StepEn { get; set; } = string.Empty;
            public string StepPrt { get; set; } = string.Empty;
            public string StepEsp { get; set; } = string.Empty;
            public int Phase { get; set; }
            public int Equipment { get; set; }
        }

        private sealed class IngredientNutrientExportModel
        {
            public int Id { get; set; }
            public string NameDe { get; set; } = string.Empty;
            public string NameEn { get; set; } = string.Empty;
            public string NamePrt { get; set; } = string.Empty;
            public string NameEsp { get; set; } = string.Empty;
            public int? GroupId { get; set; }
            public string? GroupName { get; set; }
            public int Calories_a_100g { get; set; }
            public int Weight_per_piece { get; set; }
            public decimal Fat_a_100g { get; set; }
            public decimal Saturated_fat_a_100g { get; set; }
            public decimal Carbohydrates_a_100g { get; set; }
            public decimal Sugar_a_100g { get; set; }
            public decimal Salt_a_100g { get; set; }
            public decimal Protein_a_100g { get; set; }
            public decimal Fiber_a_100g { get; set; }
        }

        private sealed class LegacyIngredientMapModel
        {
            public int OldId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int? NewId { get; set; }
        }

        public IActionResult AddNewRecipes()
        {
            return View(new AddNewRecipesModel());
        }

        public IActionResult CreatePreperationStep()
        {
            var model=_context.RecipePreperationSteps.ToList();
            return View(model);
        }

        public async Task<IActionResult> JoinIngredientPreperationStep()
        {
            var model = new JoinIngredientPreparationStepViewModel
            {
                PreparationSteps = await _context.RecipePreperationSteps.OrderBy(x => x.Id).ToListAsync(),
                Ingredients = await _context.IngredientsAndNutrients
                    .Include(x => x.Group)
                    .OrderBy(x => x.Name_DE)
                    .ToListAsync(),
                ExistingJoins = await _context.JoinIngredientPreperationStep
                    .Include(x => x.Preperation)
                    .Include(x => x.Ingredient)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJoinIngredientPreperationStep(int selectedStepId, string selectedStepIds, string selectedIngredientIds)
        {
            var stepIds = (selectedStepIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (stepIds.Count == 0 && selectedStepId > 0)
                stepIds.Add(selectedStepId);

            if (stepIds.Count == 0)
                return BadRequest("Bitte mindestens einen Zubereitungsschritt auswählen.");

            var ingredientIds = (selectedIngredientIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ingredientIds.Count == 0)
                return BadRequest("Bitte mindestens eine Zutat auswählen.");

            var existingRows = await _context.JoinIngredientPreperationStep
                .Where(x => x.Preperation != null && stepIds.Contains(x.Preperation.Id))
                .ToListAsync();

            if (existingRows.Count > 0)
                _context.JoinIngredientPreperationStep.RemoveRange(existingRows);

            var preparations = await _context.RecipePreperationSteps
                .Where(x => stepIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var ingredients = await _context.IngredientsAndNutrients
                .Where(x => ingredientIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var newRows = new List<JoinIngredientPreperationStep>();
            foreach (var stepId in stepIds)
            {
                if (!preparations.TryGetValue(stepId, out var preperation))
                    continue;

                foreach (var ingredientId in ingredientIds)
                {
                    if (!ingredients.TryGetValue(ingredientId, out var ingredient))
                        continue;

                    newRows.Add(new JoinIngredientPreperationStep
                    {
                        Preperation = preperation,
                        Ingredient = ingredient
                    });
                }
            }

            if (newRows.Count > 0)
                await _context.JoinIngredientPreperationStep.AddRangeAsync(newRows);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(JoinIngredientPreperationStep));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteJoinIngredientPreperationStep(int stepId, int ingredientId)
        {
            if (stepId <= 0 || ingredientId <= 0)
                return BadRequest("Ungültige Verknüpfung.");

            var rows = await _context.JoinIngredientPreperationStep
                .Where(x => x.Preperation != null && x.Ingredient != null && x.Preperation.Id == stepId && x.Ingredient.Id == ingredientId)
                .ToListAsync();

            if (rows.Count > 0)
            {
                _context.JoinIngredientPreperationStep.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(JoinIngredientPreperationStep));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteJoinIngredientPreperationStepsBulk(string selectedJoinIds)
        {
            var joinIds = (selectedJoinIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (joinIds.Count == 0)
                return RedirectToAction(nameof(JoinIngredientPreperationStep));

            var rows = await _context.JoinIngredientPreperationStep
                .Where(x => joinIds.Contains(x.Id))
                .ToListAsync();

            if (rows.Count > 0)
            {
                _context.JoinIngredientPreperationStep.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(JoinIngredientPreperationStep));
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientTableData()
        {
            try
            {
                var ingredients = await _context.IngredientsAndNutrients
                    .Select(x => new
                    {
                        x.Id,
                        x.Name_DE,
                     
                    })
                    .ToListAsync();

                return Json(ingredients);
            }
            catch (Exception ex)
            {
#if DEBUG
                throw new Exception("Fehler beim Laden der Zutaten-Tabelle.", ex);
#else
                return StatusCode(500, new { message = "Fehler beim Laden der Zutaten-Tabelle." });
#endif
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPreparationStepTableData(string selectedIngredientIds = "")
        {
            try
            {
                var ingredientIds = (selectedIngredientIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.TryParse(x, out var id) ? id : 0)
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

                IQueryable<RecipePreperationSteps> query = _context.RecipePreperationSteps;

                if (ingredientIds.Count > 0)
                {
                    var stepIds = await _context.JoinIngredientPreperationStep
                        .Where(x => x.Preperation != null && x.Ingredient != null && ingredientIds.Contains(x.Ingredient.Id))
                        .Select(x => x.Preperation.Id)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(x => stepIds.Contains(x.Id));
                }

                var preparationSteps = await query
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Step_DE,
                        x.Phase,
                        x.Equipment,
                        IngredientIds = _context.JoinIngredientPreperationStep
                            .Where(join => join.Preperation != null && join.Ingredient != null && join.Preperation.Id == x.Id)
                            .Select(join => join.Ingredient.Id)
                            .Distinct()
                            .ToList()
                    })
                    .ToListAsync();

                return Json(preparationSteps);
            }
            catch (Exception ex)
            {
#if DEBUG
                throw new Exception("Fehler beim Laden der Zubereitungsschritte-Tabelle.", ex);
#else
                return StatusCode(500, new { message = "Fehler beim Laden der Zubereitungsschritte-Tabelle." });
#endif
            }
        }

        public IActionResult SavePreperationStep(RecipePreperationSteps step)
        {
            _context.RecipePreperationSteps.Add(step);
            _context.SaveChanges();


            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRecipeAsync(EditRecipesModel recipe)
        {
            if (recipe.Recipes.FormFile == null)
            {
                var existingImagePath = await _context.Recipes
                    .Where(x => x.Id == recipe.Recipes.Id)
                    .Select(x => x.ImagePath)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(existingImagePath))
                {
                    var uploadResult = await _blobUploadService.UploadContentToBlobFromUrl(existingImagePath);
                    recipe.Recipes.ImagePath = uploadResult.SourceUrl;
                }
            }

            SaveNewRecipeModel saveNewRecipeModel = new()
            {
                Recipes = recipe.Recipes,
                IngredientMeasureQuantity = recipe.IngredientMeasureQuantity,
                RecipeJoyinPreperationSteps = recipe.RecipeJoyinPreperationSteps,
                Querys = recipe.Querys
            };
            await _saveNewRecipeService.SaveNewAsync(saveNewRecipeModel,false);
            return RedirectToAction("Index");

            using (var transAction = _context.Database.BeginTransaction())
            {
                try
                {


                    var querys = recipe.Querys.Split(",").ToList();

                    await _recipesService.EditRecipesAsync(recipe.Recipes.Id, recipe.Recipes);
                    await _recipesHandlerService.DeleteReciphandlerAsync(recipe.Recipes.Id);
                    await _recipesHandlerService.CreateRecipeAndIngredientHandlerAsync(recipe.Recipes.Id, recipe.IngredientHandler);
                    await _queryService.CreateQuaryHandlerAsync(recipe.Recipes.Id, querys);

                    transAction.Commit();
                }
                catch (Exception ex)
                {
                    transAction.Rollback();
                    throw new Exception($"{_myExceptions.ErrorMessage} {ex}");
                }

            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNewRecipe([FromForm] AddNewRecipesModel newRecipe)
        {
            if (newRecipe.Recipes.FormFile == null)
                return BadRequest("Bild fehlt");

            using (var transAction = _context.Database.BeginTransaction())
            {
                try
                {
                    await _blobAzureService.UploadImageToAzureBlop(newRecipe.Recipes.FormFile);
                    await _recipesService.SaveRecipesInDbAsync(newRecipe.Recipes);

                    var recipeId = await _recipesService.GetRecipeIdByNameAndPreperation(newRecipe.Recipes.Name, newRecipe.Recipes.Preparation);
                    await _queryService.CreateQuaryHandlerAsync(recipeId, newRecipe.Querys.Split(",").ToList());

                    var ingredientHandlerList = _ingredientService.GetIngredientHandlerListFromString(newRecipe.Ingredients);
                    await _recipesHandlerService.CreateRecipeAndIngredientHandlerAsync(recipeId, ingredientHandlerList);

                    if (!string.IsNullOrEmpty(newRecipe.MealPlan))
                        _mealPlanService.CreateMealPlanAsync(recipeId, newRecipe.MealPlan);

                    transAction.Commit();
                }
                catch (Exception ex)
                {
                    transAction.Rollback();
                    throw new Exception($"Hier könnte auch eine hilfreiche Fehlermeldung stehen {ex}");
                }

            }

            return RedirectToAction("Index");

        }


        public async Task<IActionResult> EditRecipesPartialView(int id)
        {
            var recipeFromDb = await _recipesService.GetRecipesFromDbByIdAsync(id);

            if (recipeFromDb == null)
                return BadRequest("Zu bearbeitendes Rezept nicht gefunden");

            var joinRows = await _context.JoinIngredientPreperationStep
                .Where(x => x.Preperation != null && x.Ingredient != null)
                .Select(x => new { StepId = x.Preperation.Id, IngredientId = x.Ingredient.Id })
                .ToListAsync();

            ViewData["StepIngredientBindings"] = joinRows
                .GroupBy(x => x.StepId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.IngredientId).Distinct().ToList());

            EditRecipesModel editRecipesModel = new()
            {
                Recipes = recipeFromDb,
                IngredientHandler = await _ingredientService.GetIngredientsByRecipesIdFromDbAsync(recipeFromDb.Id),
                Measure = await _measureService.GetMeasureFromDbAsync(),
                Querys = string.Join(",", await _queryService.GetQuerysFromDbByRecipeIdAsync(recipeFromDb.Id)),
                IngredientsAndNutrients = await _context.IngredientsAndNutrients.ToListAsync(),
                RecipePreperationSteps = await _context.RecipePreperationSteps.ToListAsync(),
                RecipeJoyinPreperationSteps = new(),
                IngredientMeasureQuantity = new()

            };

            ViewData.TemplateInfo.HtmlFieldPrefix = string.Empty;
            return View("EditRecipes", editRecipesModel);
        }

        public async Task<IActionResult> AddIngredientRow(int index)
        {
            ViewData["index"] = index;
            var listOfUnits = await _context.Metrics.Select(x => x.UnitOfMeasurement).ToListAsync();
            ViewData["unit"] = listOfUnits;

            return PartialView("_addRowIngredientPartialView", new IngredientHandlerModel());
        }


        public IActionResult AddIngredient()
        {
            var ingredient = new IngredientsAndNutrients();
            ViewData["GroupList"] = new SelectList(_context.Group, "Id", "Name");
            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(IngredientsAndNutrients model)
        {
            var exist = _context.IngredientsAndNutrients.FirstOrDefault(x => x.Name_DE.ToLower().Trim() == model.Name_DE.ToLower().Trim());
            if (ModelState.IsValid && exist == null)
            {
                model.Group = _context.Group.First(x => x.Id == int.Parse(model.Groupe));

                _context.IngredientsAndNutrients.Add(model);
                _context.SaveChanges();

                // Nach erfolgreichem Speichern weiterleiten
                return RedirectToAction("Index");
            }


            return View("AddIngredient");
        }




        public async Task<IActionResult> CreateNutriernHandlers(string nutrients, string ingredient)
        {
            await _nutrientService.CreateNutrienHandlersAsync(nutrients, ingredient);

            return RedirectToAction("Index");
        }



        public IActionResult AdditOrDeliteRecipePartialView(string query)
        {
            List<Recipes> recipes = new();
            var isNuber = int.TryParse(query, out int recipeId);
            if (isNuber)
                recipes = _context.Recipes.Where(x => x.Id == recipeId).ToList();
            else
                recipes = _context.Recipes.Where(x => x.Name.ToLower().Trim().Contains(query.ToLower().Trim())).ToList();


            return PartialView("_AdditOrDeliteRecipePartialView", recipes);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRecipes(int id)
        {
            await _recipesService.DeleteRecipesByIdAsync(id);

            return RedirectToAction("Index");

        }

        public async Task<IActionResult> DeleteSupportTicket(int id)
        {
            await _supportTicketService.DeleteSupportTicketByIdAsync(id);

            return RedirectToAction("Index");
        }
    }

}
