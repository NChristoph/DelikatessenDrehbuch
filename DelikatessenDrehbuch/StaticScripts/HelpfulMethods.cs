﻿using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.StaticScripts
{
    public static class HelpfulMethods
    {

        public static FullRecipes GetFullRecipeById(ApplicationDbContext dbContext, int recipeId)
        {
            var recipeFromDb= dbContext.Recipes.SingleOrDefault(x => x.Id == recipeId);
            var recipHandlerFromDb= dbContext.RecipesHandlers.Where(x => x.Recipe == recipeFromDb)
                                                             .Include(x=>x.IngredientHandler.Ingredient)
                                                             .Include(x=>x.IngredientHandler.Measure)
                                                             .Include(x=>x.IngredientHandler.Quantity)
                                                             .ToList();
            FullRecipes fullRecipes = new FullRecipes();
            fullRecipes.Recipes = recipeFromDb;
            fullRecipes.IngredientHandler=recipHandlerFromDb.Select(x=>x.IngredientHandler).ToList();


            return fullRecipes;
        }
    }
}
