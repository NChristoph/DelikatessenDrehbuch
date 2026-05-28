using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Services
{
    public class SearchRecipeService : ISearchRecipeService
    {
        private readonly ApplicationDbContext _context;

        private readonly IUtilityService _utilityService;

        public SearchRecipeService(ApplicationDbContext context,IUtilityService utilityService)
        {
            _context = context;
            _utilityService = utilityService;
        }
        public async Task<List<Recipes>> GetRecipesByQuery(string query)
        {
            List<Recipes> model = new();
            var filterList = _utilityService.GetListFromQueryString(query);


            model = await GetRecipesByName(query.Trim().ToLower());

            return model;
        }

        private async Task<List<Recipes>> GetRecipesByName(string query)
        {

            var recipesFromDbByName = _context.Recipes.Where(x => EF.Functions.Like(x.Name, $"%{query}%"))
                                              .AsNoTracking()
                                              .AsEnumerable()
                                              .Where(x => Regex.IsMatch(x.Name, @"\b" + Regex.Escape(query) + @"\b", RegexOptions.IgnoreCase)
                                                       || Regex.Match(x.Name, query, RegexOptions.IgnoreCase).Success)
                                              .ToList();

            var splittQueryArray = _utilityService.SplitToArrayBySeperator(query, ' ');


            var recipesFromDbByQuery = await _context.QueryHandler.Where(x => splittQueryArray.Any(t => t == x.Query.Query.ToLower()))
                                              .AsNoTracking()
                                              .Select(x => x.Recipe).ToListAsync();


            var recipesFromDbByIngredient = await _context.RecipesHandlers.Where(x => splittQueryArray.Any(t => t == x.IngredientHandler.Ingredient.Name.ToLower()))
                                              .AsNoTracking()
                                              .Select(x => x.Recipe).ToListAsync();


            var combined = recipesFromDbByName.Concat(recipesFromDbByQuery).Concat(recipesFromDbByIngredient)
                                        .GroupBy(r => r.Id)
                                        .Select(g => g.First())
                                        .ToList();

            return combined;
        }


    }
}
