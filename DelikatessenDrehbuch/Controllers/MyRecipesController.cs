using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Polly;
using SQLitePCL;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class MyRecipesController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;
        private readonly HelpfulMethods _helpfulMethods;
        public MyRecipesController(ApplicationDbContext context,HelpfulMethods helpfulMethods)
        {
            _dbcontext = context;
            _helpfulMethods = helpfulMethods;   
        }

        public IActionResult Index()
        {
          
            return View();
        }

      

        public IActionResult PremiumUserPage()
        {
            return PartialView("_PremiumUserPage");
        }

        public IActionResult Premium()
        {
            return PartialView("_premiumUserPartialView");
        }



        public IActionResult MealPlanView()
        {
            List<MealPlanModel> model = new();
            var mealPlanHandlerFromDb = _dbcontext.MealPlanHandler.Where(x => x.Id != 0)
                                                              .Include(x => x.MealPlan)
                                                              .Include(x => x.Recipes).ToList();
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

            return PartialView("_MealPlanView", model);
        }

        public ActionResult GetMeal(int id)
        {
            var recipesIds = _dbcontext.MealPlanHandler.Where(x => x.MealPlan.Id == id)
                                                                 .Select(x => x.Recipes.Id)
                                                                 .ToList();

            var ingredientHandlers = _dbcontext.RecipesHandlers.Where(rh => recipesIds
                                                         .Contains(rh.Recipe.Id))
                                                         .Include(rh => rh.IngredientHandler)
                                                         .Include(rh => rh.IngredientHandler.Ingredient)
                                                         .Include(rh => rh.IngredientHandler.Measure)
                                                         .Include(rh => rh.IngredientHandler.Quantity)
                                                         .Select(x => x.IngredientHandler)
                                                         .ToList();

            var mealModel = new MealModel();
            mealModel.MealPlan = _dbcontext.MealPlan.SingleOrDefault(x => x.Id == id);
            mealModel.Recipes=_dbcontext.Recipes.Where(x=>recipesIds.Contains(x.Id)).ToList();
            mealModel.Ingredients= ingredientHandlers.GroupBy(ih => new { ih.Ingredient.Id, ih.Measure.UnitOfMeasurement })
                                                       .Select(g => new IngredientHandlerModel
                                                       {
                                                           Ingredient = g.First().Ingredient,
                                                           Measure = g.First().Measure,
                                                           Quantity = new Quantity { Quantitys = g.Sum(ih => ih.Quantity.Quantitys) }
                                                       })
                                                       .ToList();

           
            return View("Meal",mealModel);
        }

        public IActionResult LoadMyRecipes()
        {
            if(User.IsInRole("Admin"))
            {
                var recipesFromDb = _dbcontext.Recipes.ToList();
                return PartialView("_MyRecipesPartialView", recipesFromDb);
            }

            var myRecipesFromDb = _dbcontext.Recipes.Where(x => x.OwnerEmail == User.Identity.Name).ToList();
            return PartialView("_MyRecipesPartialView", myRecipesFromDb);
        }

        public IActionResult LoadRecipesILike()
        {
            var likesFromDb = _dbcontext.Likes.Where(x => x.UserMail == User.Identity.Name).Include(x=>x.Recipe).ToList();
            var recipesFromLikes=likesFromDb.Select(x=>x.Recipe).ToList();
            return PartialView("_MyRecipesPartialView", recipesFromLikes);
        }

       
    }
}
