using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;
using Stripe;
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
                                                          .Include(rh=>rh.IngredientHandler.Ingredient.Group)
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
                                           .Include(x => x.IngredientHandler.Ingredient.Group)
                                           .Include(x => x.IngredientHandler.Quantity)
                                           .Include(x => x.IngredientHandler)
                                           .Select(x => x.IngredientHandler)
                                           .ToListAsync();


            return ingredientHandlers ?? throw new KeyNotFoundException($"Ingredienthandler vom Rezept mit RezeptId: {id} nicht gefunden");
        }

        public List<IngredientHandlerModel> GetIngredientHandlerByRecipesId(int recipesId)
        {
            var ingredientHandlerIds = StaticData.RecipeAndIngredHandlers[recipesId];
            return _context.IngredientHandlers.Where(x => ingredientHandlerIds.Contains(x.Id))
                                              .Include(x=>x.Ingredient)
                                              .Include(x => x.Measure)
                                              .Include(x => x.Quantity)
                                              .Include(x => x.Ingredient.Group)
                                              .ToList();
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
                          .FirstOrDefaultAsync(x => x.Ingredient.Name.ToLower().Trim() == ingredientHandlerModel.Ingredient.Name.ToLower().Trim()
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

        public List<IngredientHandlerModel> GetIngredientHandlerModels(List<int> ids)
        {
            return _context.IngredientHandlers.Where(x => ids.Contains(x.Id)).ToList();
        }

    }
}
