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
        public DbSet<WorldClipWatchSession> WorldClipWatchSessions { get; set; }
        public DbSet<WorldAdPreferenceProfile> WorldAdPreferenceProfiles { get; set; }
        public DbSet<WorldAdPreferenceInterest> WorldAdPreferenceInterests { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<RecipeBaseKeyword> RecipeBaseKeywords { get; set; }
        public DbSet<JoinIngredientPreperationStep> JoinIngredientPreperationStep { get; set; }
        public DbSet<MealPlanListing> MealPlanListings { get; set; }
        public DbSet<WildCoinTransaction> WildCoinTransactions { get; set; }
        public DbSet<MealPlanPurchase> MealPlanPurchases { get; set; }

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

            builder.Entity<WorldClipWatchSession>()
                .HasOne(x => x.Posting)
                .WithMany()
                .HasForeignKey(x => x.WorldUserPostingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorldClipWatchSession>()
                .HasIndex(x => new { x.CreatorUserHash, x.IsQualifiedView, x.CreatedAtUtc });

            builder.Entity<WorldClipWatchSession>()
                .HasIndex(x => new { x.ViewerUserHash, x.CreatedAtUtc });

            builder.Entity<WorldClipWatchSession>()
                .Property(x => x.CreatorUserHash)
                .HasMaxLength(256);

            builder.Entity<WorldClipWatchSession>()
                .Property(x => x.ViewerUserHash)
                .HasMaxLength(256);

            builder.Entity<WorldClipWatchSession>()
                .Property(x => x.WatchedSeconds)
                .HasColumnType("decimal(10,2)");

            builder.Entity<WorldAdPreferenceProfile>()
                .HasIndex(x => x.UserHash)
                .IsUnique();

            builder.Entity<WorldAdPreferenceProfile>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldAdPreferenceInterest>()
                .HasIndex(x => new { x.UserHash, x.InterestType, x.Score });

            builder.Entity<WorldAdPreferenceInterest>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldAdPreferenceInterest>()
                .Property(x => x.InterestType)
                .HasMaxLength(64);

            builder.Entity<WorldAdPreferenceInterest>()
                .Property(x => x.InterestKey)
                .HasMaxLength(256);

            builder.Entity<WorldAdPreferenceInterest>()
                .Property(x => x.DisplayLabel)
                .HasMaxLength(256);

            builder.Entity<WorldAdPreferenceInterest>()
                .Property(x => x.Score)
                .HasColumnType("decimal(10,2)");

            // Legacy/production table name mapping (typo kept for compatibility):
            // Model MealPlanPurchase -> dbo.WorldMealplanPurcase
            builder.Entity<MealPlanPurchase>().ToTable("WorldMealplanPurcase");
        }
    }
}
