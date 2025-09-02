using Azure.Storage.Blobs;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.MyExceptions;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRecipeAsync(EditRecipesModel recipe)
        {

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

            };

            ViewData.TemplateInfo.HtmlFieldPrefix = string.Empty;
            return View("EditRecipes", editRecipesModel);
        }

        public async Task<IActionResult> AddIngredientRow(int index)
        {
            ViewData["index"] = index;
            var listOfUnits=  await _context.Metrics.Select(x => x.UnitOfMeasurement).ToListAsync();
            ViewData["unit"] = listOfUnits;

            return PartialView("_addRowIngredientPartialView", new IngredientHandlerModel());
        }


        public IActionResult AddNutrients()
        {
            return View();
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

