using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Data
{
    public class ApplicationDbContext : IdentityDbContext, IDataProtectionKeyContext
    {
        // Persistente DataProtection-Keys (signieren/verschlüsseln u.a. die Auth-Cookies).
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

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
        public DbSet<FoodCategory> FoodCategories { get; set; }
        public DbSet<IngredientMeasureQuantity> IngredientMeasureQuantity { get; set; }
        public DbSet<RecipeBaseData> RecipeBaseData { get; set; }
        public DbSet<RecipeBaseDataImage> RecipeBaseDataImage { get; set; }
        public DbSet<RecipeJoinIngredientMeasureQuantity> RecipeJoinIngredientMeasureQuantity { get; set; }
        public DbSet<IngredientsAndNutrients> IngredientsAndNutrients { get; set; }

        public DbSet<SavedMealPlans> SavedMealPlan { get; set; }
        public DbSet<WorldAppUser> WorldAppUser { get; set; }
        public DbSet<WorldUserPosting> WorldUserPosting { get; set; }
        public DbSet<WorldUserMealPlan> WorldUserMealPlan { get; set; }
        public DbSet<WorldSharedMealPlan> WorldSharedMealPlan { get; set; }
        public DbSet<WorldSharedFeed> WorldSharedFeeds { get; set; }
        public DbSet<WorldSharedFeedMember> WorldSharedFeedMembers { get; set; }
        public DbSet<WorldSharedFeedItem> WorldSharedFeedItems { get; set; }
        public DbSet<WorldSharedFeedMessage> WorldSharedFeedMessages { get; set; }
        public DbSet<WorldSharedFeedTodo> WorldSharedFeedTodos { get; set; }
        public DbSet<WorldUserLike> WorldUserLike { get; set; }
        public DbSet<WorldUserAbo> WorldUserAbo { get; set; }
        public DbSet<WorldUserInteraction> WorldUserInteractions { get; set; }
        public DbSet<WorldUserComment> WorldUserComments { get; set; }
        public DbSet<WorldUserCommentReaction> WorldUserCommentReactions { get; set; }
        public DbSet<WorldUserCommentReport> WorldUserCommentReports { get; set; }
        public DbSet<WorldUserReport> WorldUserReports { get; set; }
        public DbSet<WorldClipWatchSession> WorldClipWatchSessions { get; set; }
        public DbSet<AgentPaymentRequest> AgentPaymentRequests { get; set; }
        public DbSet<WorldAdPreferenceProfile> WorldAdPreferenceProfiles { get; set; }
        public DbSet<WorldAdPreferenceInterest> WorldAdPreferenceInterests { get; set; }
        public DbSet<RecipeAiVariant> RecipeAiVariants { get; set; }
        public DbSet<RecipeAiVariantSelectedIngredient> RecipeAiVariantSelectedIngredients { get; set; }
        public DbSet<RecipeAiVariantIngredientRow> RecipeAiVariantIngredientRows { get; set; }
        public DbSet<RecipeAiVariantJob> RecipeAiVariantJobs { get; set; }
        public DbSet<WorldAppAd> WorldAppAds { get; set; }
        public DbSet<WorldAppAdImpression> WorldAppAdImpressions { get; set; }

        // New canonical AI recipe storage (v2)
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<RecipeBaseKeyword> RecipeBaseKeywords { get; set; }
        public DbSet<MealPlanListing> MealPlanListings { get; set; }
        public DbSet<WildCoinTransaction> WildCoinTransactions { get; set; }
        public DbSet<MealPlanPurchase> MealPlanPurchases { get; set; }
        public DbSet<WorldSharedShoppingList> WorldSharedShoppingList { get; set; }
        public DbSet<WorldUserNotification> WorldUserNotifications { get; set; }
        public DbSet<WorldUserPendingVideo> WorldUserPendingVideos { get; set; }
        public DbSet<RecipeUserVariant> RecipeUserVariants { get; set; }
        public DbSet<RecipeCommunityVariant> RecipeCommunityVariants { get; set; }

        // NEW: Simple AI Translation System (normalisiert)
        public DbSet<RecipeSteps> RecipeSteps { get; set; }

        // Trading Agents
        public DbSet<WorldTradingAgent> WorldTradingAgents { get; set; }
        public DbSet<WorldAgentTrade> WorldAgentTrades { get; set; }
        public DbSet<WorldTokenPrice> WorldTokenPrices { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // DataProtectionKeys wird per Hand-SQL-Skript verwaltet (Azure, keine
            // Migrationen) -> aus EF-Migrationen ausschließen, damit künftige
            // Migrationen nicht versuchen, die bereits existierende Tabelle anzulegen.
            builder.Entity<DataProtectionKey>().ToTable("DataProtectionKeys", t => t.ExcludeFromMigrations());

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

            builder.Entity<FoodCategory>()
                .HasIndex(x => x.CategoryKey)
                .IsUnique();

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

            builder.Entity<WorldUserComment>()
                .ToTable("WorldUserComments");

            builder.Entity<WorldUserComment>()
                .HasIndex(x => new { x.WorldUserPostingId, x.CreatedAtUtc });

            builder.Entity<WorldUserComment>()
                .HasIndex(x => new { x.UserHash, x.CreatedAtUtc });

            builder.Entity<WorldUserComment>()
                .HasIndex(x => new { x.ParentCommentId, x.CreatedAtUtc });

            builder.Entity<WorldUserComment>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldUserComment>()
                .Property(x => x.UserName)
                .HasMaxLength(120);

            builder.Entity<WorldUserComment>()
                .Property(x => x.VerificationLevel)
                .HasMaxLength(32);

            builder.Entity<WorldUserComment>()
                .Property(x => x.CommentText)
                .HasMaxLength(1200);

            builder.Entity<WorldUserCommentReaction>()
                .ToTable("WorldUserCommentReactions");

            builder.Entity<WorldUserCommentReaction>()
                .HasIndex(x => new { x.WorldUserCommentId, x.UserHash })
                .IsUnique();

            builder.Entity<WorldUserCommentReaction>()
                .HasIndex(x => new { x.WorldUserCommentId, x.IsLike });

            builder.Entity<WorldUserCommentReaction>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldUserCommentReport>()
                .ToTable("WorldUserCommentReports");

            builder.Entity<WorldUserCommentReport>()
                .HasIndex(x => new { x.WorldUserCommentId, x.UserHash })
                .IsUnique();

            builder.Entity<WorldUserCommentReport>()
                .HasIndex(x => x.CreatedAtUtc);

            builder.Entity<WorldUserCommentReport>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldUserCommentReport>()
                .Property(x => x.Reason)
                .HasMaxLength(500);

            // WorldUserReport — Content reporting system
            builder.Entity<WorldUserReport>()
                .ToTable("WorldUserReports");

            builder.Entity<WorldUserReport>()
                .HasIndex(x => new { x.PostingId, x.ReporterHash })
                .IsUnique(); // Ein User kann einen Post nur einmal melden

            builder.Entity<WorldUserReport>()
                .HasIndex(x => x.PostingId); // Für schnelles Zählen von Reports pro Post

            builder.Entity<WorldUserReport>()
                .HasIndex(x => x.ReporterHash); // Für Cooldown-Check

            builder.Entity<WorldUserReport>()
                .HasIndex(x => x.CreatedAt);

            builder.Entity<WorldUserReport>()
                .HasIndex(x => x.Status);

            builder.Entity<WorldUserReport>()
                .Property(x => x.ReporterHash)
                .HasMaxLength(128);

            builder.Entity<WorldUserReport>()
                .Property(x => x.Reason)
                .HasMaxLength(50);

            builder.Entity<WorldUserReport>()
                .Property(x => x.Status)
                .HasMaxLength(20);

            // RecipeBaseData.Title — Lookup bei Upload (SaveNewRecipeService)
            builder.Entity<RecipeBaseData>()
                .HasIndex(x => x.Title);

            builder.Entity<RecipeAiVariant>()
                .HasIndex(x => new { x.BaseRecipeId, x.VariantType, x.Language, x.AiProvider, x.SelectedIngredientKey, x.IsSharedCanonical });

            builder.Entity<RecipeAiVariant>()
                .HasIndex(x => new { x.BaseRecipeId, x.CreatedByUserHash, x.VariantType, x.Language, x.AiProvider, x.SelectedIngredientKey });

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
            builder.Entity<WorldSharedFeed>()
                .HasIndex(x => x.InviteToken)
                .IsUnique();

            builder.Entity<WorldSharedFeed>()
                .HasIndex(x => x.OwnerUserHash);

            builder.Entity<WorldSharedFeed>()
                .HasIndex(x => x.LastActivityAtUtc);

            builder.Entity<WorldSharedFeed>()
                .Property(x => x.Title)
                .HasMaxLength(160);

            builder.Entity<WorldSharedFeed>()
                .Property(x => x.OwnerUserHash)
                .HasMaxLength(256);

            builder.Entity<WorldSharedFeed>()
                .Property(x => x.InviteToken)
                .HasMaxLength(64);

            builder.Entity<WorldSharedFeedMember>()
                .HasIndex(x => new { x.WorldSharedFeedId, x.UserHash })
                .IsUnique();

            builder.Entity<WorldSharedFeedMember>()
                .HasIndex(x => x.UserHash);

            builder.Entity<WorldSharedFeedMember>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldSharedFeedMember>()
                .Property(x => x.Role)
                .HasMaxLength(32);

            builder.Entity<WorldSharedFeedMember>()
                .Property(x => x.AddedByUserHash)
                .HasMaxLength(256);

            builder.Entity<WorldSharedFeedMember>()
                .HasOne(x => x.Feed)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.WorldSharedFeedId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorldSharedFeedItem>()
                .HasIndex(x => new { x.WorldSharedFeedId, x.CreatedAtUtc });

            builder.Entity<WorldSharedFeedItem>()
                .HasIndex(x => new { x.WorldSharedFeedId, x.ContentType, x.SourceToken })
                .IsUnique();

            builder.Entity<WorldSharedFeedItem>()
                .Property(x => x.ContentType)
                .HasMaxLength(32);

            builder.Entity<WorldSharedFeedItem>()
                .Property(x => x.SourceToken)
                .HasMaxLength(64);

            builder.Entity<WorldSharedFeedItem>()
                .Property(x => x.Title)
                .HasMaxLength(200);

            builder.Entity<WorldSharedFeedItem>()
                .Property(x => x.AddedByUserHash)
                .HasMaxLength(256);

            builder.Entity<WorldSharedFeedItem>()
                .HasOne(x => x.Feed)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.WorldSharedFeedId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorldUserNotification>()
                .ToTable("WorldUserNotifications");

            builder.Entity<WorldUserNotification>()
                .HasIndex(x => new { x.UserHash, x.IsSeen, x.CreatedAtUtc });

            builder.Entity<WorldUserNotification>()
                .HasIndex(x => new { x.UserHash, x.NotificationKey });

            builder.Entity<WorldUserNotification>()
                .Property(x => x.UserHash)
                .HasMaxLength(256);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.Icon)
                .HasMaxLength(64);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.Sender)
                .HasMaxLength(128);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.Href)
                .HasMaxLength(600);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.NotificationKey)
                .HasMaxLength(300);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.EventType)
                .HasMaxLength(64);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.LatestActorName)
                .HasMaxLength(128);

            builder.Entity<WorldUserNotification>()
                .Property(x => x.ContextText)
                .HasMaxLength(400);

            builder.Entity<WorldSharedShoppingList>()
                .HasIndex(x => x.ShareToken);

            // Legacy/production table name mapping (typo kept for compatibility):
            // Model MealPlanPurchase -> dbo.WorldMealplanPurcase
            builder.Entity<MealPlanPurchase>().ToTable("WorldMealplanPurcase");

            // --- Naming-Compatibility: C#-Klassen umbenannt, DB-Schema bleibt ---

            builder.Entity<FoodCategory>(e =>
            {
                e.ToTable("food_categories");
                e.Property(x => x.CategoryKey)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("Category_Key");
                e.Property(x => x.Name_DE).HasMaxLength(255);
                e.Property(x => x.Name_EN).HasMaxLength(255);
                e.Property(x => x.Name_PRT).HasMaxLength(255);
                e.Property(x => x.Name_ESP).HasMaxLength(255);
                e.Property(x => x.Name_ID).HasMaxLength(255);
                e.Property(x => x.Name_NL).HasMaxLength(255);
                e.Property(x => x.Name_SE).HasMaxLength(255);
                e.Property(x => x.Name_DK).HasMaxLength(255);
                e.Property(x => x.Name_NO).HasMaxLength(255);
                e.Property(x => x.Name_MS).HasMaxLength(255);
            });

            builder.Entity<IngredientsAndNutrients>(e =>
            {
                e.HasOne(x => x.FoodCategory)
                    .WithMany(x => x.Ingredients)
                    .HasForeignKey(x => x.FoodCategoryId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Ingredients_FoodCategories");
            });

            builder.Entity<RecipeAiVariant>(e =>
            {
                e.ToTable("RecipeAiVariants");
                e.Property(x => x.VariantType).IsRequired().HasMaxLength(64);
                e.Property(x => x.Language).IsRequired().HasMaxLength(12);
                e.Property(x => x.AiProvider).IsRequired().HasMaxLength(32);
                e.Property(x => x.SelectedIngredientKey).IsRequired().HasMaxLength(400);
                e.Property(x => x.CreatedByUserHash).HasMaxLength(256);
                e.Property(x => x.LatestUserNote).HasMaxLength(1000);
                e.Property(x => x.RenderedTitle).IsRequired().HasMaxLength(256);
                e.Property(x => x.RenderedSummary).HasMaxLength(4000);
                e.Property(x => x.PreparationText).HasMaxLength(16000);
                e.Property(x => x.IngredientsText).HasMaxLength(16000);
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

                e.HasMany(x => x.SelectedIngredients)
                    .WithOne(x => x.RecipeAiVariant)
                    .HasForeignKey(x => x.RecipeAiVariantId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasMany(x => x.IngredientRows)
                    .WithOne(x => x.RecipeAiVariant)
                    .HasForeignKey(x => x.RecipeAiVariantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RecipeAiVariantSelectedIngredient>(e =>
            {
                e.ToTable("RecipeAiVariantSelectedIngredients");
                e.HasIndex(x => new { x.RecipeAiVariantId, x.SortOrder });
                e.HasIndex(x => new { x.RecipeAiVariantId, x.IngredientId }).IsUnique();

                e.HasOne(x => x.Ingredient)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<RecipeAiVariantIngredientRow>(e =>
            {
                e.ToTable("RecipeAiVariantIngredientRows");
                e.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
                e.Property(x => x.QuantityText).HasMaxLength(128);
                e.HasIndex(x => new { x.RecipeAiVariantId, x.SortOrder });
                e.HasIndex(x => x.IngredientMeasureQuantityId);

                e.HasOne(x => x.Ingredient)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientId)
                    .OnDelete(DeleteBehavior.NoAction);

                e.HasOne(x => x.Measure)
                    .WithMany()
                    .HasForeignKey(x => x.MeasureId)
                    .OnDelete(DeleteBehavior.NoAction);

                e.HasOne(x => x.IngredientMeasureQuantity)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientMeasureQuantityId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // REMOVED: RecipeAiBaseRecipe* entities - feature deprecated in favor of RecipeUserVariants

            builder.Entity<RecipeAiVariantJob>(e =>
            {
                e.ToTable("RecipeAiVariantJobs");
                e.Property(x => x.JobId).IsRequired().HasMaxLength(64);
                e.Property(x => x.VariantType).IsRequired().HasMaxLength(64);
                e.Property(x => x.Language).IsRequired().HasMaxLength(12);
                e.Property(x => x.AiProvider).IsRequired().HasMaxLength(32);
                e.Property(x => x.State).IsRequired().HasMaxLength(32);
                e.Property(x => x.Message).HasMaxLength(400);
                e.Property(x => x.Title).HasMaxLength(256);
                e.Property(x => x.CreatedByUserHash).HasMaxLength(256);

                e.HasIndex(x => x.JobId).IsUnique();
                e.HasIndex(x => new { x.BaseRecipeId, x.UpdatedAtUtc });

                e.HasOne(x => x.BaseRecipe)
                    .WithMany()
                    .HasForeignKey(x => x.BaseRecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RecipeCommunityVariant>(e =>
            {
                e.ToTable("RecipeCommunityVariants");
                e.Property(x => x.Title).IsRequired().HasMaxLength(256);
                e.Property(x => x.Summary).HasMaxLength(400);
                e.Property(x => x.Language).IsRequired().HasMaxLength(12);
                e.Property(x => x.CreatedByUserHash).IsRequired().HasMaxLength(256);

                e.HasIndex(x => new { x.OriginalRecipeId, x.CreatedAtUtc });

                e.HasOne(x => x.OriginalRecipe)
                    .WithMany()
                    .HasForeignKey(x => x.OriginalRecipeId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.ParentCommunityVariant)
                    .WithMany()
                    .HasForeignKey(x => x.ParentCommunityVariantId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Queries>().ToTable("Querys");
            builder.Entity<UserPreferencesQuery>().ToTable("UserPreferencesQuerys");

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
