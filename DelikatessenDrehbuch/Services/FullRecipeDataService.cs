using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class FullRecipeDataService : IFullRecipeDataService
    {
        private readonly IIngredientScaleService _ingredientService;
        private readonly IRecipesService _recipesService;
        private readonly INutrientService _nutrientService;

        private readonly ApplicationDbContext _context;
        public FullRecipeDataService(IIngredientScaleService ingredientService,IRecipesService recipesService,
                                     ApplicationDbContext context,INutrientService nutrientService)
        {
            _ingredientService = ingredientService;
            _recipesService = recipesService;
            _context = context;
            _nutrientService = nutrientService;
        }

        public Task<FullRecipeData> GetFullRecipeDataAsync(int id)
        {
            throw new NotImplementedException();
        }
    }
}
