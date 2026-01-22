using Azure.Storage.Blobs;
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



        public AdminController(ApplicationDbContext context, AddRecipeException myExceptions,
                               IRecipesService recipesService, IAdminControllerModelService adminControllerModelService,
                               IBlobAzureService blobAzureService, IIngredientService ingredientService,
                               IQueryService queryService, IRecipesHandlerService recipesHandlerService,
                               IMeasureService measureService, IQuantityService quantityService,
                               IMealPlanService mealPlanService, INutrientService nutrientService,
                               ISupportTicketService supportTicketService)
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

            return View(new RecipePreperationSteps());
        }

        public IActionResult SavePreperationStep(RecipePreperationSteps step)
        {
            _context.RecipePreperationSteps.Add(step);
            _context.SaveChanges();


            return RedirectToAction("Index");
        }

       
        //TODO:Doppel speicherung verhindern

 
        public void SaveNew(EditRecipesModel recipesModel)
        {
            RecipeBaseData recipeBaseData;

            // 1. Rezept suchen oder erstellen
            var existingRecipeBaseData = _context.RecipeBaseData
                   .FirstOrDefault(r => r.Title == recipesModel.Recipes.Name);

            if (existingRecipeBaseData == null)
            {
                // --- NEUES REZEPT ---
                recipeBaseData = new RecipeBaseData
                {
                    Title = recipesModel.Recipes.Name,
                    PersonCount = (int)recipesModel.Recipes.RecipePersonCount,
                    Preferences = recipesModel.Querys,
                    Category = recipesModel.Recipes.Category,
                    PreperationTime = (int)recipesModel.Recipes.PreparationTime
                };
               
                _context.RecipeBaseData.Add(recipeBaseData);
                 _context.SaveChanges();
            }
            else
            {
                // --- EXISTIERENDES REZEPT ---
                recipeBaseData = existingRecipeBaseData;

                // Werte aktualisieren (Beispiel)
                recipeBaseData.PersonCount = (int)recipesModel.Recipes.RecipePersonCount;
                // EF Core weiß durch das Laden schon, dass dieses Objekt existiert (State = Modified/Unchanged)
            }

            // --- ZUTATEN VERARBEITEN ---
            foreach (var ingredien in recipesModel.IngredientMeasureQuantity)
            {
                IngredientMeasureQuantity ingredientToUseForJoin;

                // Prüfen ob Zutat in DB existiert
                var exist = _context.IngredientMeasureQuantity
                     .FirstOrDefault(x => x.IngredientsAndNutrients.Name_DE == ingredien.IngredientsAndNutrients.Name_DE
                                          && x.Measure.UnitOfMeasurement == ingredien.Measure.UnitOfMeasurement
                                          && x.Quantity.Quantitys == ingredien.Quantity.Quantitys);

                if (exist == null)
                {
                    // Neue Zutat erstellen
                    if (ingredien.Measure.UnitOfMeasurement == "Gramm")
                        ingredien.Measure.UnitOfMeasurement = "g.";

                    var newIng = new IngredientMeasureQuantity
                    {
                        // Hier müssen wir aufpassen: Die Referenzen müssen aus dem Context kommen, 
                        // sonst versucht er die auch neu anzulegen!
                        IngredientsAndNutrients = _context.IngredientsAndNutrients.FirstOrDefault(x => x.Id == ingredien.IngredientsAndNutrients.Id),
                        Measure = _context.Metrics.FirstOrDefault(x => x.UnitOfMeasurement.ToLower() == ingredien.Measure.UnitOfMeasurement.ToLower()),
                        Quantity = _context.Quantities.FirstOrDefault(x => x.Quantitys == ingredien.Quantity.Quantitys)
                    };

                    // Validate that the related lookups were found to avoid FK violations
                    if (newIng.IngredientsAndNutrients == null)
                        throw new InvalidOperationException($"IngredientsAndNutrients with Id={ingredien.IngredientsAndNutrients.Id} not found.");
                    if (newIng.Measure == null)
                        throw new InvalidOperationException($"Measure '{ingredien.Measure.UnitOfMeasurement}' not found.");
                    if (newIng.Quantity == null)
                        throw new InvalidOperationException($"Quantity '{ingredien.Quantity.Quantitys}' not found.");

                    _context.IngredientMeasureQuantity.Add(newIng); // Zum Speichern vormerken
                    ingredientToUseForJoin = newIng;
                }
                else
                {
                    ingredientToUseForJoin = exist;
                }

               
                var recipeJoynIng = new RecipeJoinIngredientMeasureQuantity()
                {
                    Recipe = recipeBaseData,
                    Ingredient = ingredientToUseForJoin
                };

                _context.RecipeJoinIngredientMeasureQuantity.Add(recipeJoynIng);
            }

          

            _context.SaveChanges();
          

            // --- SCHRITTE HINZUFÜGEN ---
            foreach (var step in recipesModel.RecipeJoyinPreperationSteps)
            {
                
                step.Recipe = recipeBaseData; // Auch hier einfach das Objekt verknüpfen
                step.RecipePreperationStep = _context.RecipePreperationSteps.First(x=>x.Id==step.PreperationStepId);
                _context.RecipeJoinPreperationSteps.Add(step);
            }

            RecipeBaseDataImage recipeBaseDataImage = new RecipeBaseDataImage
            {
                Recipe = recipeBaseData,
                Image = _context.Recipes.Where(x=>x.Preparation==recipesModel.Recipes.Preparation).Select(x=>x.ImagePath).First()
            };
            _context.RecipeBaseDataImage.Add(recipeBaseDataImage);

            // --- DAS GROSSE FINALE ---
            // Hier wird ALLES in der richtigen Reihenfolge gespeichert.
            // Erst Rezept -> bekommt ID -> dann Joins und Schritte mit dieser ID.
            _context.SaveChanges();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRecipeAsync(EditRecipesModel recipe)
        {
            SaveNew(recipe);
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

