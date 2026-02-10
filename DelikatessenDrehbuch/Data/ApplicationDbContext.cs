using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
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
        public DbSet<IngredientMeasureQuantity> IngredientMeasureQuantity { get; set; }
        public DbSet<RecipeBaseData> RecipeBaseData { get; set; }
        public DbSet<RecipeBaseDataImage> RecipeBaseDataImage { get; set; }
        public DbSet<RecipeJoinIngredientMeasureQuantity> RecipeJoinIngredientMeasureQuantity { get; set; }
        public DbSet<RecipeJoyinPreperationSteps> RecipeJoinPreperationSteps { get; set; }
        public DbSet<IngredientsAndNutrients> IngredientsAndNutrients { get; set; }
        public DbSet<RecipePreperationSteps> RecipePreperationSteps { get; set; }
       
        public DbSet<SavedMealPlans> SavedMealPlan { get; set; }
        public DbSet<WorldAppUser> WorldAppUser { get; set; }
        public DbSet<WorldUserPosting> WorldUserPosting { get; set; }
        public DbSet<WorldUserMealPlan> WorldUserMealPlan { get; set; }
        public DbSet<WorldSharedMealPlan> WorldSharedMealPlan { get; set; }
        public DbSet<WorldUserLike> WorldUserLike { get; set; }
        public DbSet<WorldUserAbo> WorldUserAbo { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<RecipeBaseKeyword> RecipeBaseKeywords { get; set; }
        public DbSet<JoinIngredientPreperationStep> JoinIngredientPreperationStep { get; set; }


        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RecipeBaseKeyword>()
                .HasKey(link => new { link.RecipeBaseDataId, link.KeywordId });

            builder.Entity<RecipeBaseKeyword>()
                .HasOne(link => link.RecipeBaseData)
                .WithMany(recipe => recipe.RecipeKeywords)
                .HasForeignKey(link => link.RecipeBaseDataId);

            builder.Entity<RecipeBaseKeyword>()
                .HasOne(link => link.Keyword)
                .WithMany(keyword => keyword.RecipeLinks)
                .HasForeignKey(link => link.KeywordId);

        }
    }
}
