using FoodForFun.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodForFun.Data
{
    public class ApplicationDbContext:DbContext
    {
        public DbSet<Recipes> Recipes { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Quantity> Quantities { get; set; }
        public DbSet<RecipesHandler> RecipesHandlers { get; set; }
        

        public ApplicationDbContext(DbContextOptions options)
             : base(options)
        {
            
        }
    }
}
