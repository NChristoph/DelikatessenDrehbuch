using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly HelpfulMethods _helpfulMethods;

        public AdminController(ApplicationDbContext context, IMemoryCache cache,HelpfulMethods helpfulMethods)
        {
            _context = context;
            _cache = cache;
            _helpfulMethods = helpfulMethods;
        }
        public IActionResult Index()
        {
            AdminControllerModel model = new();
            model.SupportMessage = _context.SupportMessage.ToList();
            model.Recipes = _context.Recipes.ToList();
            model.UserCount = _context.Users.Count();
            model.PremiumUser = _context.Users.Where(user => _context.UserRoles
                                              .Any(ur => ur.UserId == user.Id && _context.Roles
                                              .Any(r => r.Id == ur.RoleId && r.Name == "PremiumUser")))
                                              .Count();

            return View(model);
        }

        public IActionResult EditRecipesPartialView(int id)
        {
            var fullRecipes = _helpfulMethods.GetFullRecipeById(_context,id);  
            return View("EditRecipes",fullRecipes);
        }
        public IActionResult Test(int id)
        {
            DropdownModel dropdownModel = new DropdownModel();
            dropdownModel.IngredientHandler = new IngredientHandlerModel();
            dropdownModel.Measure=_context.Metrics.ToList();
            dropdownModel.Index=id;


            return PartialView("_IngredientPartialView", dropdownModel);
        }

        //TODO:RezeptBearbeiten noch machen
        public IActionResult EditeRecipes(FullRecipes fullRecipes)
        {
            return RedirectToAction("Index");
        }
       

       // [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult AdditOrDeliteRecipePartialView(string query)
        {
           // string cacheKey = $"{User.Identity.Name}_handlers_{query?.ToLower()}";
            List<Recipes> recipes = new List<Recipes>();
            int recipeId;
            var isNuber = int.TryParse(query, out recipeId);
            if (isNuber)
                recipes = _context.Recipes.Where(x => x.Id == recipeId).ToList();
            else
                recipes =_context.Recipes.Where(x=>x.Name.ToLower().Trim().Contains(query.ToLower().Trim())).ToList();


           // var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Setzt das Caching-Timeout auf 10 Minuten (anpassbar)

            //_cache.Set(cacheKey, recipes, cacheEntryOptions);



            return PartialView("_AdditOrDeliteRecipePartialView", recipes);
        }

        [HttpPost]
        public IActionResult DeleteRecipes(int id)
        {

            var recipesFromDb = _context.Recipes.SingleOrDefault(x => x.Id == id);
            var recessionFromDb = _context.Recessions.Where(x => x.Recipes.Id == id).ToList();
            var queryHandlerFromDb = _context.QueryHandler.Where(x => x.Recipe.Id == id).ToList();
            var userPreverenceRecipeFromDb = _context.UserPreferencesRecipes.SingleOrDefault(x => x.Recipes.Id == id);

            if (recipesFromDb == null)
                return BadRequest();
            if (userPreverenceRecipeFromDb != null)
                _context.UserPreferencesRecipes.Remove(userPreverenceRecipeFromDb);

            if (recessionFromDb != null)
                foreach (var recession in recessionFromDb)
                    _context.Remove(recession);

            if (queryHandlerFromDb != null)
                foreach (var queryHandler in queryHandlerFromDb)
                    _context.QueryHandler.Remove(queryHandler);


            _context.Remove(recipesFromDb);
            _context.SaveChanges();

            return RedirectToAction("Index");

        }

        public IActionResult DeleteSupportTicket(int id)
        {
            if (id == 0)
                return BadRequest("No Id Found");
            var supportMessageFromDb = _context.SupportMessage.SingleOrDefault(x => x.Id == id);

            if (supportMessageFromDb == null)
                return BadRequest("No Message Fund");

            _context.Remove(supportMessageFromDb);
            _context.SaveChanges();


            return RedirectToAction("Index");
        }


    }
}
