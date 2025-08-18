using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class NutrientService:INutrientService
    {
        private readonly ApplicationDbContext _context;
        private readonly IIngredientService _ingredientService;
        private readonly IUtilityService _utilityService;
        private readonly IQuantityService _quantityService;
        private readonly IMeasureService _measureService;

        public NutrientService(ApplicationDbContext context, IIngredientService ingredientService,IUtilityService utilityService, IQuantityService quantityService,IMeasureService measureService )
        {
            _context = context;
            _ingredientService = ingredientService;
            _utilityService = utilityService;
            _quantityService = quantityService;
            _measureService = measureService;
        }

        public async Task CreateNutrienHandlersAsync(string nutrients, string ingredient)
        {
            string exceptionString = "";
            using (var transAction = _context.Database.BeginTransaction())
            {
                try
                {
                    var ingredientFromDb = _ingredientService.GetIngredientByNameFromDb(ingredient);
                    var nutrientsArray = _utilityService.SplitLinesToArray(nutrients);
                   
                    List<NutrienHandler> nutrienHandlers = new();

                    for (int i = 0; i < nutrientsArray.Length; i++)
                    {
                        exceptionString=nutrientsArray[i];
                        var nutrienHandler = await CreateNutrienHandler(nutrientsArray[i].Split("#").ToArray(), ingredientFromDb);
                        nutrienHandlers.Add(nutrienHandler);

                    }

                    await _context.NutrienHandler.AddRangeAsync(nutrienHandlers);
                    await _context.SaveChangesAsync();

                    transAction.Commit();
                }
                catch (Exception ex)
                {
                    transAction.Rollback();
                    throw new Exception($"Beim Erstellen der Inhaltsstoffe für {ingredient} ist in der Zeile {exceptionString} ein Fehler aufgetreten.",ex);
                    
                }
            }
           
            
        }

        public async Task<NutrienHandler> CreateNutrienHandler(string[] nutrienSplitted,Ingredient ingredientFromDb)
        {
            NutrienHandler nutrienHandler = new()
            {
                Ingredient = ingredientFromDb,
                Nutrients = await GetNutrientsFromDb(nutrienSplitted[0]),
                Quantity = await _quantityService.GetOrCreateQuantityAsync(double.Parse(nutrienSplitted[1])),
                Metrics = await _measureService.GetorCreateMeasureAsync(nutrienSplitted[2])
            };

            return nutrienHandler;
        }

        private async Task<Nutrients> GetNutrientsFromDb(string nutrient)
        {
            var NutrientsFromDb = await _context.Nutrients.SingleOrDefaultAsync(x => x.Nutrient.ToLower().Trim() == nutrient.ToLower().Trim());

            if (NutrientsFromDb == null)
                throw new Exception($"Nutrient mit den Namen: {nutrient} existierst nicht.");

            return NutrientsFromDb;
        }

        public Task<List<NutrienHandler>> GetNutrienHandlersByIngredientNamesAsync(List<string> ingredientNames)
        {
           return _context.NutrienHandler.Where(x => ingredientNames.Contains(x.Ingredient.Name.ToLower()))
                                                           .Include(x => x.Ingredient)
                                                           .Include(x => x.Quantity)
                                                           .Include(x => x.Nutrients).ToListAsync();
        }
    }
}
