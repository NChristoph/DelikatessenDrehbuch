using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Polly;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class MyRecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        private readonly IIngredientService _ingredientService;
        private readonly ILikeService _likeService;
        private readonly IMeasureService _measureService;
        public MyRecipesController(ApplicationDbContext context, HelpfulMethods helpfulMethods, IIngredientService ingredientService, ILikeService likeService, IMeasureService measureService)
        {
            _context = context;
            _helpfulMethods = helpfulMethods;
            _ingredientService = ingredientService;
            _likeService = likeService;
            _measureService = measureService;
        }

        public IActionResult Index()
        {
            MyRecipesModel model = new()
            {
                QueryFirstList = _context.MealplanFilter.Select(x => x.Filter).ToList(),
                IngredientList = _context.Ingredients.ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> AddIngredientRowAsync(int index,string name,int indexValue)
        {
            ViewData["index"] = index;
            ViewData["indexValue"] = indexValue;
            var measure = await _measureService.GetMeasureFromDbAsync();
            var listOfUnits = measure.Select(x=>x.UnitOfMeasurement).ToList();
            ViewData["unit"] = listOfUnits;

            return PartialView("_addRowPartial", new IngredientHandlerModel() { Ingredient = new() { Name=name} });
        }



        public IActionResult PremiumUserPage()
        {
            return PartialView("_PremiumUserPage");
        }

        public IActionResult Premium()
        {
            return PartialView("_premiumUserPartialView");
        }

        //TODO: Mach das ordentlich
        public IActionResult FilterMenues([FromBody] List<string> categories)
        {
            List<PreMadeMenuesModel> model = new();

            if (!User.IsInRole("PremiumUser"))
            {

                var notPremiumModel = _context.MealPlanHandler.Where(x => x.Id != 0)
                                                               .Include(x => x.MealPlan)
                                                               .Include(x => x.Recipes)
                                                               .ToList();

                var groupedPlans = notPremiumModel.GroupBy(x => x.MealPlan);

                foreach (var mealPlan in groupedPlans)
                {
                    PreMadeMenuesModel mealPlanModel = new();
                    mealPlanModel.MealPlan = mealPlan.Key;
                    foreach (var recipes in mealPlan)
                    {
                        mealPlanModel.Recipes.Add(recipes.Recipes);
                    }
                    model.Add(mealPlanModel);
                }
                model.Take(10);

                return PartialView("_mealPlansPartialView", model);

            }

            var mealPlanHandlerFromDb = _context.MealPlanHandler.Where(x => x.Id != 0 && categories.Contains(x.MealPlan.MyMealModel.Category))
                                                              .Include(x => x.MealPlan)
                                                              .Include(x => x.Recipes)
                                                              .ToList();

            var groupedMealPlans = mealPlanHandlerFromDb.GroupBy(x => x.MealPlan);
            foreach (var mealpan in groupedMealPlans)
            {
                PreMadeMenuesModel mealPlanModel = new();
                mealPlanModel.MealPlan = mealpan.Key;

                foreach (var recipes in mealpan)
                {
                    mealPlanModel.Recipes.Add(recipes.Recipes);
                }

                model.Add(mealPlanModel);
            }
            return PartialView("_mealPlansPartialView", model);
        }
        public IActionResult MealPlanView()
        {
            var categoryFromDb = _context.MyMealModel.Select(x => x.Category).ToList();


            return PartialView("_MealPlanView", categoryFromDb);
        }

        public ActionResult GetMeal(int id)
        {
            var recipesIds = _context.MealPlanHandler.Where(x => x.MealPlan.Id == id)
                                                                 .Select(x => x.Recipes.Id)
                                                                 .ToList();



            var mealModel = new MealModel();
            mealModel.MealPlan = _context.MealPlan.SingleOrDefault(x => x.Id == id);
            mealModel.Recipes = _context.Recipes.Where(x => recipesIds.Contains(x.Id)).ToList();

            mealModel.Ingredients = _ingredientService.GetIngredientsByRecipesIdsList(recipesIds);


            return View("Meal", mealModel);
        }



        public IActionResult LoadRecipesILike()
        {
            var recipesILikeFromDb = _context.Likes.Where(x => x.UserMail == User.Identity.Name).Include(x => x.Recipe).Select(x => x.Recipe).ToList();

            return PartialView("_MyRecipesPartialView", recipesILikeFromDb);
        }

        public async Task<IActionResult> RemoveRecipeFromLikedList(int recipeId)
        {
            var recipesILikeFromDb = _context.Likes.Where(x => x.UserMail == User.Identity.Name).Select(x => x.Recipe).ToList();
            var recipe = recipesILikeFromDb.First(x => x.Id == recipeId);
            var currentUserName = User.Identity.Name;
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);
            await _likeService.RemoveLikeAsync(like);
           


            return  RedirectToAction("Index");
        }


    }
}
