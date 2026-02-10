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
                Ingredients = await _context.IngredientsAndNutrients.OrderBy(x => x.Name_DE).ToListAsync(),
                ExistingJoins = await _context.JoinIngredientPreperationStep
                    .Include(x => x.Preperation)
                    .Include(x => x.Ingredient)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJoinIngredientPreperationStep(int selectedStepId, string selectedIngredientIds)
        {
            if (selectedStepId <= 0)
                return BadRequest("Bitte einen Zubereitungsschritt auswählen.");

            var ingredientIds = (selectedIngredientIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ingredientIds.Count == 0)
                return BadRequest("Bitte mindestens eine Zutat auswählen.");

            var existingRows = await _context.JoinIngredientPreperationStep
                .Where(x => x.Preperation.Id == selectedStepId)
                .ToListAsync();

            if (existingRows.Count > 0)
                _context.JoinIngredientPreperationStep.RemoveRange(existingRows);

           
            var newRows = new List<JoinIngredientPreperationStep>();
            foreach (var ingredientId in ingredientIds)
            {
                var preperation = await _context.RecipePreperationSteps.FindAsync(selectedStepId);
                var ingredient = await _context.IngredientsAndNutrients.FindAsync(ingredientId);
                newRows.Add(new JoinIngredientPreperationStep
                {
                    Preperation = preperation,
                    Ingredient = ingredient
                });
            }

            await _context.JoinIngredientPreperationStep.AddRangeAsync(newRows);
            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> GetPreparationStepTableData()
        {
            try
            {
                var preparationSteps = await _context.RecipePreperationSteps
                    .Select(x => new
                    {
                        x.Id,
                        x.Step_DE
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
