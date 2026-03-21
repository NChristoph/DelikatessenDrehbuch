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

            var preparationSteps = await _context.RecipePreparationSteps
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
                .OrderBy(x => x.Id)
                .Select(x => new IngredientNutrientExportModel
                {
                    Id = x.Id,
                    Name = x.Name_DE
                })
                .ToListAsync();

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
                    "name"
                }
            };

            var payload = new
            {
                exported_at_utc = DateTime.UtcNow,
                record_count = exportRecipes.Count,
                schema,
                recipes = exportRecipes,
                preparation_steps = preparationSteps,
                ingredients_and_nutrients = ingredientsAndNutrients
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

            TempData["ExportJsonMessage"] = $"JSON Export erstellt: data/exports/{fileName}";
            return RedirectToAction(nameof(Index));
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
            public string Name { get; set; } = string.Empty;
        }

        public IActionResult AddNewRecipes()
        {
            return View(new AddNewRecipesModel());
        }

        public IActionResult CreatePreparationStep()
        {
            var model=_context.RecipePreparationSteps.ToList();
            return View(model);
        }

        public async Task<IActionResult> JoinIngredientPreparationStep()
        {
            var model = new JoinIngredientPreparationStepViewModel
            {
                PreparationSteps = await _context.RecipePreparationSteps.OrderBy(x => x.Id).ToListAsync(),
                Ingredients = await _context.IngredientsAndNutrients
                    .Include(x => x.Group)
                    .OrderBy(x => x.Name_DE)
                    .ToListAsync(),
                ExistingJoins = await _context.JoinIngredientPreparationStep
                    .Include(x => x.Preparation)
                    .Include(x => x.Ingredient)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJoinIngredientPreparationStep(int selectedStepId, string selectedStepIds, string selectedIngredientIds)
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

            var existingRows = await _context.JoinIngredientPreparationStep
                .Where(x => x.Preparation != null && stepIds.Contains(x.Preparation.Id))
                .ToListAsync();

            if (existingRows.Count > 0)
                _context.JoinIngredientPreparationStep.RemoveRange(existingRows);

            var preparations = await _context.RecipePreparationSteps
                .Where(x => stepIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var ingredients = await _context.IngredientsAndNutrients
                .Where(x => ingredientIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var newRows = new List<JoinIngredientPreparationStep>();
            foreach (var stepId in stepIds)
            {
                if (!preparations.TryGetValue(stepId, out var preparation))
                    continue;

                foreach (var ingredientId in ingredientIds)
                {
                    if (!ingredients.TryGetValue(ingredientId, out var ingredient))
                        continue;

                    newRows.Add(new JoinIngredientPreparationStep
                    {
                        Preparation = preparation,
                        Ingredient = ingredient
                    });
                }
            }

            if (newRows.Count > 0)
                await _context.JoinIngredientPreparationStep.AddRangeAsync(newRows);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(JoinIngredientPreparationStep));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteJoinIngredientPreparationStep(int stepId, int ingredientId)
        {
            if (stepId <= 0 || ingredientId <= 0)
                return BadRequest("Ungültige Verknüpfung.");

            var rows = await _context.JoinIngredientPreparationStep
                .Where(x => x.Preparation != null && x.Ingredient != null && x.Preparation.Id == stepId && x.Ingredient.Id == ingredientId)
                .ToListAsync();

            if (rows.Count > 0)
            {
                _context.JoinIngredientPreparationStep.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(JoinIngredientPreparationStep));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteJoinIngredientPreparationStepsBulk(string selectedJoinIds)
        {
            var joinIds = (selectedJoinIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (joinIds.Count == 0)
                return RedirectToAction(nameof(JoinIngredientPreparationStep));

            var rows = await _context.JoinIngredientPreparationStep
                .Where(x => joinIds.Contains(x.Id))
                .ToListAsync();

            if (rows.Count > 0)
            {
                _context.JoinIngredientPreparationStep.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(JoinIngredientPreparationStep));
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

                IQueryable<RecipePreparationSteps> query = _context.RecipePreparationSteps;

                if (ingredientIds.Count > 0)
                {
                    var stepIds = await _context.JoinIngredientPreparationStep
                        .Where(x => x.Preparation != null && x.Ingredient != null && ingredientIds.Contains(x.Ingredient.Id))
                        .Select(x => x.Preparation.Id)
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
                        IngredientIds = _context.JoinIngredientPreparationStep
                            .Where(join => join.Preparation != null && join.Ingredient != null && join.Preparation.Id == x.Id)
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

        public IActionResult SavePreparationStep(RecipePreparationSteps step)
        {
            _context.RecipePreparationSteps.Add(step);
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
                RecipeJoinPreparationSteps = recipe.RecipeJoinPreparationSteps,
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

                    var recipeId = await _recipesService.GetRecipeIdByNameAndPreparation(newRecipe.Recipes.Name, newRecipe.Recipes.Preparation);
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

            var joinRows = await _context.JoinIngredientPreparationStep
                .Where(x => x.Preparation != null && x.Ingredient != null)
                .Select(x => new { StepId = x.Preparation.Id, IngredientId = x.Ingredient.Id })
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
                RecipePreparationSteps = await _context.RecipePreparationSteps.ToListAsync(),
                RecipeJoinPreparationSteps = new(),
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
