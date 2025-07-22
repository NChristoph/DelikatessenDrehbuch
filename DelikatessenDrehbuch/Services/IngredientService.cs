using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Polly;
using System.Security.Policy;

namespace DelikatessenDrehbuch.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly ApplicationDbContext _context;
        public IngredientService(ApplicationDbContext context)
        {
            _context = context;
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
    }
}
