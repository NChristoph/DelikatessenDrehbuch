using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;

namespace DelikatessenDrehbuch.Controllers
{
    public class SelectedRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRecipesService _recipesService;
        private readonly IQueryService _queryService;
        private readonly INutrientService _nutrientService;
        private readonly IIngredientService _ingredientService;
        private readonly ILikeService _likeService;
        private readonly IRecessionsService _recessionsService;

        public SelectedRecipeController(ApplicationDbContext dbContext, IRecipesService recipesService,
                                        IQueryService queryService, INutrientService nutrientService,
                                        IIngredientService ingredientService, ILikeService likeService,
                                        IRecessionsService recessionsService)
        {
            _context = dbContext;
            _recipesService = recipesService;
            _queryService = queryService;
            _nutrientService = nutrientService;
            _ingredientService = ingredientService;
            _likeService = likeService;
            _recessionsService = recessionsService;
        }


        public async Task<IActionResult> Index(int id, string name)
        {
            var userIsLoggedIn = User.Identity.IsAuthenticated;

            var querys = await _queryService.GetQuerysFromDbByRecipeIdAsync(id);
            var recipesIds = await _context.QueryHandler.Where(x => x.Query.Query.ToLower().Trim() == querys.First().ToLower().Trim())
                                                        .Select(x => x.Recipe.Id)
                                                        .ToListAsync();

            var randomRecipesIds = _recipesService.GetRendomRecipesIdsByCountFromIdListAsync(recipesIds, 7);

            SelectedRecipesModel model = new()
            {
                FullRecipeData = await _recipesService.GetFullRecipeDataByRecipesIdAsync(id),
                Querys = querys,
                NutrienHandlers = await _nutrientService.GetNutrienHandlersByIngredientNamesAsync(
                                       await _ingredientService.GetIngredientsNamesByRecipesId(id)),

                RecipeSuggestions = await _context.Recipes.Where(x => randomRecipesIds.Contains(x.Id))
                                                          .ToListAsync()
            };

            return View(model);
        }



        public async Task<IActionResult> AddOrRemoveLike(int id)
        {
            var currentUserName = User.Identity.Name;
            var recipe = await _recipesService.GetRecipesFromDbByIdAsync(id);
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);

            if (recipe == null)
                return BadRequest("Rezept nicht gefunden.");

            if (like == null)
                await _likeService.CreateLikeAsync(id, currentUserName);
            else
                await _likeService.RemoveLikeAsync(like);

            return RedirectToAction("Index", new { id });
        }

       




        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecessionInDB(int id, string assessment)
        {
            await _recessionsService.SaveNewRecessionInDbAsync(id, assessment, User.Identity.Name);

            return RedirectToAction("Index", new { id });
        }
    }
}
