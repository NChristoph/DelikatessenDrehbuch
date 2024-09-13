using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace DelikatessenDrehbuch.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<Recipes> Recipes { get; set; }
        public DbSet<Recession> Recessions { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Quantity> Quantities { get; set; }
        public DbSet<Measure> Metrics { get; set; }
      
        public DbSet<RecipesHandler> RecipesHandlers { get; set; }
        public DbSet<IngredientHandlerModel> IngredientHandlers { get; set; }
        public DbSet<SupportMessage> SupportMessage { get; set; }
        public DbSet<Querys> Querys { get; set; }
        public DbSet<QueryHandler> QueryHandler { get; set; }
        public DbSet<UserPreferencesQuery> UserPreferencesQuerys { get; set; }
        public DbSet<UserPreferencesRecipe> UserPreferencesRecipes { get; set; }
        public DbSet<WeeklyRecipe> WeeklyRecipe { get; set; }
        public DbSet<WeeklyRecipeHandler> WeeklyRecipeHandler { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
           
        }
    }
}