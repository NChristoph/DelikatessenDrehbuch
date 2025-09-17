using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;
using System.Security.Policy;

namespace DelikatessenDrehbuch.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly ApplicationDbContext _context;
        private readonly IQuantityService _quantityService;
        private readonly IMeasureService _measureService;
        private readonly IUtilityService _utilityService;
        public IngredientService(ApplicationDbContext context, IQuantityService quantityService, IMeasureService measureService, IUtilityService utilityService)
        {
            _context = context;
            _quantityService = quantityService;
            _measureService = measureService;
            _utilityService = utilityService;
        }
        public List<IngredientHandlerModel> GetIngredientsByRecipesIdsList(List<int> ids)
        {
            var ingredientHandlers = _context.RecipesHandlers.Where(rh => ids
                                                          .Contains(rh.Recipe.Id))
                                                          .Include(rh => rh.IngredientHandler)
                                                          .Include(rh => rh.IngredientHandler.Ingredient)
                                                          .Include(rh => rh.IngredientHandler.Measure)
                                                          .Include(rh => rh.IngredientHandler.Quantity)
                                                          .Select(x => x.IngredientHandler);



            var sortedIngredientHandler = ingredientHandlers.GroupBy(ih => new { ih.Ingredient.Id, ih.Measure.UnitOfMeasurement })
                                                            .Select(g => new IngredientHandlerModel
                                                            {
                                                                Ingredient = g.First().Ingredient,
                                                                Measure = g.First().Measure,
                                                                Quantity = new Quantity { Quantitys = g.Sum(ih => ih.Quantity.Quantitys) }
                                                            }).ToList();


            return sortedIngredientHandler;
        }

        public async Task<List<IngredientHandlerModel>> GetIngredientsByRecipesIdFromDbAsync(int id)
        {
            var ingredientHandlers = await _context.RecipesHandlers.Where(x => x.Recipe.Id == id)
                                           .Include(x => x.IngredientHandler.Ingredient)
                                           .Include(x => x.IngredientHandler.Measure)
                                           .Include(x => x.IngredientHandler.Quantity)
                                           .Select(x => x.IngredientHandler)
                                           .ToListAsync();

            return ingredientHandlers ?? throw new KeyNotFoundException($"Ingredienthandler vom Rezept mit RezeptId: {id} nicht gefunden");
        }

        public Ingredient GetIngredientByNameFromDb(string name)
        {
            return _context.Ingredients.SingleOrDefault(x => x.Name.ToLower() == name.ToLower().Trim());
        }


        public List<IngredientHandlerModel> GetIngredientHandlerListFromString(string mapToIngredientHandlers)
        {
            var ingredients = _utilityService.SplitLinesToArray(mapToIngredientHandlers);
            List<IngredientHandlerModel> ingredientHandlerModels = new();
            foreach (var ingredient in ingredients)
            {
                if (!string.IsNullOrEmpty(ingredient))
                {
                    string[] ing = ingredient.Split("#");
                    IngredientHandlerModel ingredientHandler = new();
                    ingredientHandler.Id = 0;
                    ingredientHandler.Ingredient.Name = ing[2].Trim();
                    ingredientHandler.Measure.UnitOfMeasurement = ing[1].Trim();
                    ingredientHandler.Quantity.Quantitys = double.Parse(ing[0].Trim());

                    ingredientHandlerModels.Add(ingredientHandler);
                }


            }

            return ingredientHandlerModels;
        }

        public async Task<IngredientHandlerModel> GetOrCreateIngredientHandlerAsync(IngredientHandlerModel ingredientHandlerModel)
        {
            var handler = await _context.IngredientHandlers
                          .SingleOrDefaultAsync(x => x.Ingredient.Name.ToLower().Trim() == ingredientHandlerModel.Ingredient.Name.ToLower().Trim()
                          &&
                             (
                                 x.Measure.UnitOfMeasurement.Trim().ToLower() == ingredientHandlerModel.Measure.UnitOfMeasurement.Trim().ToLower()
                              || x.Measure.UnitOfMeasurement.Trim().ToLower() == ingredientHandlerModel.Measure.UnitOfMeasurement.Trim().ToLower() + "."
                              )
                          && x.Quantity.Quantitys == ingredientHandlerModel.Quantity.Quantitys);

            if (handler != null)
                return handler;

            handler = new IngredientHandlerModel()
            {
                Id = 0,
                Ingredient = await GetOrCreateIngredient(ingredientHandlerModel.Ingredient.Name),
                Quantity = await _quantityService.GetOrCreateQuantityAsync(ingredientHandlerModel.Quantity.Quantitys),
                Measure = await _measureService.GetorCreateMeasureAsync(ingredientHandlerModel.Measure.UnitOfMeasurement)
            };

            return handler;
        }



        private async Task<Ingredient> GetOrCreateIngredient(string ingredientName)
        {
            var ingredientFromDb = await _context.Ingredients.SingleOrDefaultAsync(x => x.Name.ToLower().Trim() == ingredientName.ToLower().Trim());

            if (ingredientFromDb != null)
                return ingredientFromDb;
            else
            {
                ingredientFromDb = new Ingredient()
                {
                    Id = 0,
                    Name = ingredientName.Trim(),

                };

                _context.Add(ingredientFromDb);
                await _context.SaveChangesAsync();
            }

            return ingredientFromDb;
        }

        public async Task<List<string>> GetIngredientsNamesByRecipesId(int id)
        {
            return await _context.RecipesHandlers.Where(x => x.Recipe.Id == id)
                                                 .Select(x => x.IngredientHandler.Ingredient.Name)
                                                 .ToListAsync();
        }

        public List<IngredientHandlerModel> CombineIngredienthanderModel(List<IngredientHandlerModel> listToSort)
        {
            var combined = listToSort.GroupBy(ih => new { ih.Ingredient.Id, Unit = ih.Measure.UnitOfMeasurement })
                                     .Select(g =>
                                     {
                                         var first = g.First();
                                         var unit = first.Measure.UnitOfMeasurement;

                                         bool isG = unit.Equals("g.", StringComparison.OrdinalIgnoreCase);
                                         bool isMl = unit.Equals("ml", StringComparison.OrdinalIgnoreCase);
                                         bool isB = unit.Equals("Blatt", StringComparison.OrdinalIgnoreCase);
                                         var avg = first.Ingredient?.AverageWeight ?? 0d; // ggf. AverageWeightGrams

                                         // Wenn Einheit nicht g/ml und Ø-Gewicht vorhanden -> in g umrechnen
                                         var sum = (!isG && !isMl && avg > 0d)
                                             ? g.Sum(x => x.Quantity.Quantitys) * avg
                                             : g.Sum(x => x.Quantity.Quantitys);

                                         var outUnit = (!isG && !isMl && !isB && avg > 0d) ? "g." : unit;

                                         return new IngredientHandlerModel
                                         {
                                             Ingredient = first.Ingredient,
                                             Measure = new Measure { UnitOfMeasurement = outUnit },
                                             Quantity = new Quantity { Quantitys = sum }
                                         };
                                     })
                                     .ToList();

            return combined;




        }
    }
}
