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
        public DbSet<Queries> Querys { get; set; }
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
        public DbSet<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
        public DbSet<RecipeJoinSmartStep> RecipeJoinSmartStep { get; set; }
        public DbSet<IngredientsAndNutrients> IngredientsAndNutrients { get; set; }
        public DbSet<RecipePreparationSteps> RecipePreparationSteps { get; set; }
        public DbSet<SmartRecipeStep> SmartRecipeStep { get; set; }

        public DbSet<SavedMealPlans> SavedMealPlan { get; set; }
        public DbSet<WorldAppUser> WorldAppUser { get; set; }
        public DbSet<WorldUserPosting> WorldUserPosting { get; set; }
        public DbSet<WorldUserMealPlan> WorldUserMealPlan { get; set; }
        public DbSet<WorldSharedMealPlan> WorldSharedMealPlan { get; set; }
        public DbSet<WorldUserLike> WorldUserLike { get; set; }
        public DbSet<WorldUserBookmark> WorldUserBookmark { get; set; }
        public DbSet<WorldUserAbo> WorldUserAbo { get; set; }
        public DbSet<WorldClipWatchSession> WorldClipWatchSessions { get; set; }
        public DbSet<WorldAdPreferenceProfile> WorldAdPreferenceProfiles { get; set; }
        public DbSet<WorldAdPreferenceInterest> WorldAdPreferenceInterests { get; set; }
        public DbSet<RecipeAiVariant> RecipeAiVariants { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<RecipeBaseKeyword> RecipeBaseKeywords { get; set; }
        public DbSet<JoinIngredientPreparationStep> JoinIngredientPreparationStep { get; set; }
        public DbSet<MealPlanListing> MealPlanListings { get; set; }
        public DbSet<WildCoinTransaction> WildCoinTransactions { get; set; }
        public DbSet<MealPlanPurchase> MealPlanPurchases { get; set; }
        public DbSet<WorldSharedShoppingList> WorldSharedShoppingList { get; set; }

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

            // WorldAppUser — fast jede Query filtert nach UserHash
            builder.Entity<WorldAppUser>()
                .HasIndex(x => x.UserHash)
                .IsUnique();

            // WorldUserMealPlan — alle Queries filtern nach UserHash
            builder.Entity<WorldUserMealPlan>()
                .HasIndex(x => x.UserHash);

            // WorldUserMealPlan — UserHash + Title Kombination (SaveNewMealPlan, EditMealPlan)
            builder.Entity<WorldUserMealPlan>()
                .HasIndex(x => new { x.UserHash, x.Title });

            // WorldUserPosting — Feed-Queries filtern nach CreatorId
            builder.Entity<WorldUserPosting>()
                .HasIndex(x => x.CreatorId);

            // RecipeBaseData.Title — Lookup bei Upload (SaveNewRecipeService)
            builder.Entity<RecipeBaseData>()
                .HasIndex(x => x.Title);

            builder.Entity<SmartRecipeStep>()
                .HasIndex(x => new { x.MasterStepKey, x.Phase, x.Equipment });

            builder.Entity<RecipeAiVariant>()
                .HasIndex(x => new { x.BaseRecipeId, x.VariantType, x.Language, x.IsSharedCanonical });

            builder.Entity<RecipeAiVariant>()
                .HasIndex(x => new { x.BaseRecipeId, x.CreatedByUserHash, x.VariantType, x.Language });

            // MealPlanPurchase — Duplikatschutz via TxHash
            builder.Entity<MealPlanPurchase>()
                .HasIndex(x => x.ReferenceTxHash)
                .IsUnique();

            // MealPlanPurchase — Käufer-Abfragen
            builder.Entity<MealPlanPurchase>()
                .HasIndex(x => x.BuyerHash);

            // MealPlanPurchase — Verkäufer-Dashboard
            builder.Entity<MealPlanPurchase>()
                .HasIndex(x => x.SellerHash);

            // MealPlanListings — aktive Listings (Marketplace)
            builder.Entity<MealPlanListing>()
                .HasIndex(x => new { x.IsActive, x.CreatedAt });

            // MealPlanListings — Verkäufer
            builder.Entity<MealPlanListing>()
                .HasIndex(x => x.SellerHash);

            // WildCoinTransaction — User-History
            builder.Entity<WildCoinTransaction>()
                .HasIndex(x => new { x.UserHash, x.CreatedAt });

            // WorldSharedShoppingList — Token-Lookup
            builder.Entity<WorldSharedShoppingList>()
                .HasIndex(x => x.ShareToken);

            // Legacy/production table name mapping (typo kept for compatibility):
            // Model MealPlanPurchase -> dbo.WorldMealplanPurcase
            builder.Entity<MealPlanPurchase>().ToTable("WorldMealplanPurcase");

            // --- Naming-Compatibility: C#-Klassen umbenannt, DB-Schema bleibt ---

            builder.Entity<RecipePreparationSteps>().ToTable("RecipePreperationSteps");

            builder.Entity<RecipeJoinPreparationSteps>(e =>
            {
                e.ToTable("RecipeJoinPreperationSteps");
                e.HasOne(x => x.RecipePreparationStep)
                    .WithMany()
                    .HasForeignKey("RecipePreperationStepId");
            });

            builder.Entity<SmartRecipeStep>(e =>
            {
                e.ToTable("SmartRecipeSteps");
                e.Property(x => x.MasterStepKey).IsRequired().HasMaxLength(128);
                e.Property(x => x.VariablesJson).IsRequired();
                e.Property(x => x.Equipment).HasMaxLength(256);
            });

            builder.Entity<RecipeJoinSmartStep>(e =>
            {
                e.ToTable("RecipeJoinSmartSteps");
                e.HasOne(x => x.Recipe)
                    .WithMany(x => x.SmartSteps)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.SmartRecipeStep)
                    .WithMany(x => x.RecipeLinks)
                    .HasForeignKey(x => x.SmartRecipeStepId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => new { x.RecipeId, x.StepIndex });
            });

            builder.Entity<RecipeAiVariant>(e =>
            {
                e.ToTable("RecipeAiVariants");
                e.Property(x => x.VariantType).IsRequired().HasMaxLength(64);
                e.Property(x => x.Language).IsRequired().HasMaxLength(12);
                e.Property(x => x.CreatedByUserHash).HasMaxLength(256);
                e.Property(x => x.LatestUserNote).HasMaxLength(1000);
                e.Property(x => x.RenderedTitle).IsRequired().HasMaxLength(256);
                e.Property(x => x.RenderedSummary).HasMaxLength(4000);
                e.Property(x => x.StepPlanJson).IsRequired();
                e.Property(x => x.RenderedStepsJson).IsRequired();
                e.Property(x => x.RenderedIngredientsJson).IsRequired();
                e.Property(x => x.RenderedHighlightsJson).IsRequired();

                e.HasOne(x => x.BaseRecipe)
                    .WithMany()
                    .HasForeignKey(x => x.BaseRecipeId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.ParentVariant)
                    .WithMany()
                    .HasForeignKey(x => x.ParentVariantId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Queries>().ToTable("Querys");
            builder.Entity<UserPreferencesQuery>().ToTable("UserPreferencesQuerys");

            builder.Entity<JoinIngredientPreparationStep>(e =>
            {
                e.ToTable("JoinIngredientPreperationStep");
                e.HasOne(x => x.Preparation)
                    .WithMany()
                    .HasForeignKey("PreperationId");
            });

            builder.Entity<Measure>(e =>
            {
                e.Property(m => m.Metrics_DE).HasColumnName("Metriks_DE");
                e.Property(m => m.Metrics_EN).HasColumnName("Metriks_EN");
                e.Property(m => m.Metrics_ESP).HasColumnName("Metriks_ESP");
                e.Property(m => m.Metrics_PRT).HasColumnName("Metriks_PRT");
                e.Property(m => m.Metrics_ID).HasColumnName("Metriks_ID");
                e.Property(m => m.Metrics_MS).HasColumnName("Metriks_MS");
                e.Property(m => m.Metrics_NL).HasColumnName("Metriks_NL");
                e.Property(m => m.Metrics_SE).HasColumnName("Metriks_SE");
                e.Property(m => m.Metrics_DK).HasColumnName("Metriks_DK");
                e.Property(m => m.Metrics_NO).HasColumnName("Metriks_NO");
            });

            builder.Entity<RecipeBaseData>()
                .Property(e => e.PreparationTime).HasColumnName("PreperationTime");
        }
    }
}
