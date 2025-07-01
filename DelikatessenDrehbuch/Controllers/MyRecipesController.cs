using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class MyRecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        public MyRecipesController(ApplicationDbContext context, HelpfulMethods helpfulMethods)
        {
            _context = context;
            _helpfulMethods = helpfulMethods;
        }

        public IActionResult Index()
        {
            var querylist = _context.MealplanFilter.Select(x => x.Filter).ToList();

            return View(querylist);
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
            List<MealPlanModel> model = new();

            if (!User.IsInRole("PremiumUser"))
            {

                var notPremiumModel = _context.MealPlanHandler.Where(x => x.Id != 0)
                                                               .Include(x => x.MealPlan)
                                                               .Include(x => x.Recipes)
                                                               .ToList();

                var groupedPlans = notPremiumModel.GroupBy(x => x.MealPlan);

                foreach (var mealPlan in groupedPlans)
                {
                    MealPlanModel mealPlanModel = new();
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
                MealPlanModel mealPlanModel = new();
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
            var categoryFromDb =  _context.MyMealModel.Select(x => x.Category).ToList();


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

            mealModel.Ingredients = _helpfulMethods.GetIngredientsByRecipesIdsList(_context, recipesIds);


            return View("Meal", mealModel);
        }

        public IActionResult LoadMyRecipes()
        {
            if (User.IsInRole("Admin"))
            {
                var recipesFromDb = _context.Recipes.ToList();
                return PartialView("_MyRecipesPartialView", recipesFromDb);
            }

            var myRecipesFromDb = _context.Recipes.Where(x => x.OwnerEmail == User.Identity.Name).ToList();
            return PartialView("_MyRecipesPartialView", myRecipesFromDb);
        }

        public IActionResult LoadRecipesILike()
        {
            var likesFromDb = _context.Likes.Where(x => x.UserMail == User.Identity.Name).Include(x => x.Recipe).ToList();
            var recipesFromLikes = likesFromDb.Select(x => x.Recipe).ToList();
            return PartialView("_MyRecipesPartialView", recipesFromLikes);
        }


    }
}
