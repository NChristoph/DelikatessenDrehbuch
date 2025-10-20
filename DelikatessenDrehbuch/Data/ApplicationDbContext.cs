using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
        public DbSet<MealPlan> MealPlan { get; set; }
        public DbSet<MealPlanHandler> MealPlanHandler { get; set; }
        public DbSet<MyMealModel> MyMealModel { get; set; }
        public DbSet<MealplanFilter> MealplanFilter { get; set; }
        public DbSet<Nutrients> Nutrients { get; set; }
        public DbSet<NutrienHandler> NutrienHandler { get; set; }
        public DbSet<Group> Group { get; set; }


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