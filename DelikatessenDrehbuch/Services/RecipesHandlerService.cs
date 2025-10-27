using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services
{
    public class RecipesHandlerService : IRecipesHandlerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IIngredientService _ingredientService;
        public RecipesHandlerService(ApplicationDbContext context, IIngredientService ingredientService)
        {
            _context = context;
            _ingredientService = ingredientService;
        }
        public async Task CreateRecipeAndIngredientHandlerAsync(int recipesId, List<IngredientHandlerModel> ingredientHandlers)
        {
            var recipesFromDb = await _context.Recipes.SingleOrDefaultAsync(x=>x.Id==recipesId);

            List<RecipesHandler> newReciphandler = new();

            foreach (var handler in ingredientHandlers)
            {
                if (string.IsNullOrEmpty(handler.Measure.UnitOfMeasurement))
                    handler.Measure.UnitOfMeasurement = "Stk.";

                RecipesHandler newHandler = new()
                {
                    Id = 0,
                    Recipe = recipesFromDb,
                    IngredientHandler = await _ingredientService.GetOrCreateIngredientHandlerAsync(handler)
                };
                newReciphandler.Add(newHandler);
            }
            _context.AddRange(newReciphandler);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteReciphandlerAsync(int recipesId)
        {
            var recipeHandlersFromDb = await GetRecipesHandlerByRecipesIdAsync(recipesId);

            _context.RecipesHandlers.RemoveRange(recipeHandlersFromDb);
            await _context.SaveChangesAsync();
        }

        public async Task<List<RecipesHandler>> GetRecipesHandlerByRecipesIdAsync(int recipesId)
        {
            return await _context.RecipesHandlers.Where(x => x.Recipe.Id == recipesId)
                                                 .Include(x => x.IngredientHandler.Ingredient)
                                                 .Include(x => x.IngredientHandler.Measure)
                                                 .Include(x => x.IngredientHandler.Quantity)
                                                 .Include(x=>x.IngredientHandler.Ingredient.Group)
                                                 .Include(x=>x.Recipe)
                                                 .ToListAsync();

            

        }

        public IQueryable<RecipesHandler> GetRecipesHandlerByRecipesIdsAsync(List<int> recipesIds)
        {
            return _context.RecipesHandlers.Where(x => recipesIds.Contains(x.Recipe.Id))
                                                     .Include(x => x.IngredientHandler)
                                                     .Include(x=>x.Recipe)
                                                     .AsNoTracking()
                                                     .AsQueryable();
        }
    }
}
