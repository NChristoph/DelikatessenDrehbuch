using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions
{
    public static class RecipeQueryExtensions
    {
        public static IQueryable<RecipeBaseData> IncludeFullRecipeDetails(this IQueryable<RecipeBaseData> query)
        {
            return query
                .Include(r => r.Images)
                .Include(r => r.Steps)
                    .ThenInclude(s => s.RecipePreperationStep)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                            .ThenInclude(n => n.Group)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity);
        }

        public static IQueryable<RecipeBaseData> IncludeIngredients(this IQueryable<RecipeBaseData> query)
        {
            return query
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                            .ThenInclude(n => n.Group)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity);
        }

        public static IQueryable<WorldUserPosting> IncludeFullPostingRecipe(this IQueryable<WorldUserPosting> query)
        {
            return query
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(link => link.Ingredient)
                            .ThenInclude(i => i.IngredientsAndNutrients)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(link => link.Ingredient)
                            .ThenInclude(i => i.Measure)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(link => link.Ingredient)
                            .ThenInclude(i => i.Quantity)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Images)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Steps)
                        .ThenInclude(s => s.RecipePreperationStep)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.RecipeKeywords);
        }
    }
}
