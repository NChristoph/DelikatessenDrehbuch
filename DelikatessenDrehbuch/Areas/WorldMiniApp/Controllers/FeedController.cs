using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class FeedController : Controller
    {
        private const string WorldMiniAppAdminHash = "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839";
        private const int CommentAutoHideReportThreshold = 3;
        private const string SessionWalletWLD = "WorldWallet_WLD";
        private const string SessionWalletUSDT = "WorldWallet_USDT";
        private static readonly SemaphoreSlim EnsureCommentsSchemaLock = new(1, 1);
        private static volatile bool CommentsSchemaEnsured = false;
        private static readonly SemaphoreSlim EnsureNotificationsSchemaLock = new(1, 1);
        private static volatile bool NotificationsSchemaEnsured = false;
        private const string EnsureWorldUserCommentsSchemaSql = @"
IF OBJECT_ID(N'[dbo].[WorldUserComments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserComments](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserComments] PRIMARY KEY,
        [WorldUserPostingId] INT NOT NULL,
        [ParentCommentId] INT NULL,
        [UserHash] NVARCHAR(256) NOT NULL,
        [UserName] NVARCHAR(120) NOT NULL CONSTRAINT [DF_WorldUserComments_UserName] DEFAULT (N'User'),
        [VerificationLevel] NVARCHAR(32) NOT NULL CONSTRAINT [DF_WorldUserComments_VerificationLevel] DEFAULT (N''),
        [CommentText] NVARCHAR(1200) NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserComments_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_WorldUserComments_IsDeleted] DEFAULT (0)
    );

    CREATE INDEX [IX_WorldUserComments_WorldUserPostingId_CreatedAtUtc]
        ON [dbo].[WorldUserComments]([WorldUserPostingId], [CreatedAtUtc]);

    CREATE INDEX [IX_WorldUserComments_UserHash_CreatedAtUtc]
        ON [dbo].[WorldUserComments]([UserHash], [CreatedAtUtc]);
END;

IF COL_LENGTH(N'[dbo].[WorldUserComments]', N'ParentCommentId') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserComments]
        ADD [ParentCommentId] INT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_WorldUserComments_ParentCommentId_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'[dbo].[WorldUserComments]')
)
BEGIN
    CREATE INDEX [IX_WorldUserComments_ParentCommentId_CreatedAtUtc]
        ON [dbo].[WorldUserComments]([ParentCommentId], [CreatedAtUtc]);
END;

IF COL_LENGTH(N'[dbo].[WorldUserComments]', N'IsPinned') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserComments]
        ADD [IsPinned] BIT NOT NULL CONSTRAINT [DF_WorldUserComments_IsPinned] DEFAULT (0);
END;

IF OBJECT_ID(N'[dbo].[WorldUserCommentReactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserCommentReactions](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserCommentReactions] PRIMARY KEY,
        [WorldUserCommentId] INT NOT NULL,
        [UserHash] NVARCHAR(256) NOT NULL,
        [IsLike] BIT NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserCommentReactions_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserCommentReactions_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE INDEX [IX_WorldUserCommentReactions_WorldUserCommentId_UserHash]
        ON [dbo].[WorldUserCommentReactions]([WorldUserCommentId], [UserHash]);

    CREATE INDEX [IX_WorldUserCommentReactions_WorldUserCommentId_IsLike]
        ON [dbo].[WorldUserCommentReactions]([WorldUserCommentId], [IsLike]);
END;

IF OBJECT_ID(N'[dbo].[WorldUserCommentReports]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserCommentReports](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserCommentReports] PRIMARY KEY,
        [WorldUserCommentId] INT NOT NULL,
        [UserHash] NVARCHAR(256) NOT NULL,
        [Reason] NVARCHAR(500) NOT NULL CONSTRAINT [DF_WorldUserCommentReports_Reason] DEFAULT (N''),
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserCommentReports_CreatedAtUtc] DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE INDEX [IX_WorldUserCommentReports_WorldUserCommentId_UserHash]
        ON [dbo].[WorldUserCommentReports]([WorldUserCommentId], [UserHash]);

    CREATE INDEX [IX_WorldUserCommentReports_CreatedAtUtc]
        ON [dbo].[WorldUserCommentReports]([CreatedAtUtc]);
END;
";
        private const string EnsureWorldUserNotificationsSchemaSql = @"
IF OBJECT_ID(N'[dbo].[WorldUserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserNotifications](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserNotifications] PRIMARY KEY,
        [UserHash] NVARCHAR(256) NOT NULL,
        [Icon] NVARCHAR(64) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Icon] DEFAULT (N'bi-bell'),
        [Sender] NVARCHAR(128) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Sender] DEFAULT (N'system'),
        [Description] NVARCHAR(1000) NOT NULL,
        [Href] NVARCHAR(600) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserNotifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsSeen] BIT NOT NULL CONSTRAINT [DF_WorldUserNotifications_IsSeen] DEFAULT (0),
        [SeenAtUtc] DATETIME2 NULL
    );

    CREATE INDEX [IX_WorldUserNotifications_UserHash_IsSeen_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [IsSeen], [CreatedAtUtc]);
END;
";
        private static readonly Dictionary<string, string[]> CategoryAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["appetizer"] = new[] { "appetizer", "aperetizer", "vorspeise", "entrada" },
            ["main"] = new[] { "main", "maincourse", "hauptspeise", "platoprincipal", "pratoprincipal" },
            ["dessert"] = new[] { "dessert", "postre", "sobremesa", "nachspeise" }
        };

        private static readonly Dictionary<string, string> CategoryAliasLookup = CategoryAliases
            .SelectMany(group => group.Value.Select(alias => new KeyValuePair<string, string>(alias, group.Key)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _context;
        private readonly ILogger<FeedController> _logger;
        private readonly IWildCoinService _coinService;
        private readonly IConfiguration _configuration;

        public FeedController(ApplicationDbContext context, ILogger<FeedController> logger, IWildCoinService coinService, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _coinService = coinService;
            _configuration = configuration;
        }
        //TODO:Likecount zu basedata recipe hinzuf�gen und abo system auch machen neue column auserdem brauchen 
        //wir noch eine ide damit die likes rot sind wen wir sie geliket haben
        //TodoThumbAutomatisch speichern
        public async Task<IActionResult> Index(string filter = "feed", string userHash = "", int scrollToId = 0, string searchTerm = "", string category = "", int? maxPrepTime = null)
        {
            userHash = ResolveUserHash(userHash);
            await EnsureWorldUserCommentsSchemaAsync();
            List<WorldUserPosting> model = new List<WorldUserPosting>();

            
            var baseQuery = _context.WorldUserPosting
                .AsNoTracking()
                .Include(p => p.Recipe)
                .ThenInclude(r => r.RecipeKeywords)
                .ThenInclude(link => link.Keyword);

            IQueryable<WorldUserPosting> query = baseQuery.Where(p => !p.IsOffline);

            switch (filter)
            {
                case "likes":
                    if (!string.IsNullOrEmpty(userHash))
                    {
                        var currentUser = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
                        if (currentUser != null)
                        {
                            var likes = _context.WorldUserLike.Where(x => x.WorldAppUser.UserHash == currentUser.UserHash).Select(x=>x.Recipe.Id);

                            query = baseQuery.Where(x => likes.Contains(x.Recipe.Id));
                        }
                        else
                        {
                            query = baseQuery.Where(_ => false);
                        }
                    }
                    else
                    {
                        query = baseQuery.Where(_ => false);
                    }
                    break;

                case "myvideos":
                    if (!string.IsNullOrEmpty(userHash))
                    {
                        query = baseQuery
                            .Where(p => p.CreatorId == userHash);
                    }
                    else
                    {
                        query = baseQuery.Where(_ => false);
                    }
                    break;

                case "feed":
                default:
                    break;
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var trimmedSearchTerm = searchTerm.Trim();
                var searchPattern = $"%{trimmedSearchTerm}%";
                var canonicalSearchCategory = ResolveCanonicalCategory(trimmedSearchTerm);
                var categoryTerms = canonicalSearchCategory is null
                    ? null
                    : CategoryAliases[canonicalSearchCategory];

                query = query.Where(post => EF.Functions.Like(post.Recipe.Title, searchPattern)
                    || EF.Functions.Like(post.Recipe.Category, searchPattern)
                    || (categoryTerms != null
                        && post.Recipe.Category != null
                        && categoryTerms.Contains(post.Recipe.Category.ToLower()))
                    || post.Recipe.RecipeKeywords.Any(link =>
                        EF.Functions.Like(link.Keyword.Word_DE, searchPattern)
                        || EF.Functions.Like(link.Keyword.Word_EN, searchPattern)
                        || EF.Functions.Like(link.Keyword.Word_ESP, searchPattern)
                        || EF.Functions.Like(link.Keyword.Word_PRT, searchPattern)));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var canonicalCategory = ResolveCanonicalCategory(category);

                if (canonicalCategory is null)
                {
                    var trimmedCategory = category.Trim();
                    var categoryPattern = $"%{trimmedCategory}%";
                    query = query.Where(post => EF.Functions.Like(post.Recipe.Category, categoryPattern));
                }
                else
                {
                    var canonicalCategoryTerms = CategoryAliases[canonicalCategory];
                    query = query.Where(post => post.Recipe.Category != null
                        && canonicalCategoryTerms.Contains(post.Recipe.Category.ToLower()));
                }
            }

            if (maxPrepTime.HasValue)
            {
                query = query.Where(post => post.Recipe.PreparationTime <= maxPrepTime.Value);
            }

            if (filter == "myvideos")
            {
                query = query.OrderByDescending(post => post.CreationTime);
            }
            else
            {
                query = query.OrderByDescending(post => post.Id);
            }

            if (filter == "feed" && string.IsNullOrWhiteSpace(searchTerm) && string.IsNullOrWhiteSpace(category) && !maxPrepTime.HasValue)
            {
                query = query.Take(20);
            }

            model = await query.ToListAsync();

            foreach (var item in model)
            {
               item.Source=ChangePath(item.Source);
                item.ThumbnailUrl=ChangePath(item.ThumbnailUrl);
            }

            // Gelikte Recipe-IDs des aktuellen Users laden (f�r initial roten Herz-Zustand)
            var likedRecipeIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(userHash))
            {
                likedRecipeIds = (await _context.WorldUserLike
                    .AsNoTracking()
                    .Where(l => l.WorldAppUser.UserHash == userHash)
                    .Select(l => l.Recipe.Id)
                    .ToListAsync()).ToHashSet();
            }
            ViewData["LikedRecipeIds"] = likedRecipeIds;

            // Like-Counts für die geladenen Rezepte
            var feedRecipeIds = model.Where(m => m.Recipe != null).Select(m => m.Recipe.Id).Distinct().ToList();
            var likeCounts = feedRecipeIds.Count > 0
                ? await _context.WorldUserLike
                    .AsNoTracking()
                    .Where(l => feedRecipeIds.Contains(l.Recipe.Id))
                    .GroupBy(l => l.Recipe.Id)
                    .Select(g => new { RecipeId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.RecipeId, x => x.Count)
                : new Dictionary<int, int>();
            ViewData["LikeCounts"] = likeCounts;

            var postingIds = model.Select(m => m.Id).Distinct().ToList();
            ViewData["CommentCounts"] = await GetCommentCountsForPostingsAsync(postingIds);
            ViewData["CanWriteComments"] = await CanWriteCommentsAsync(userHash);

            ViewData["ScrollToId"] = scrollToId;
            ViewData["CurrentFilter"] = filter;
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Category"] = category;
            ViewData["MaxPrepTime"] = maxPrepTime?.ToString() ?? string.Empty;
            ViewData["UserHash"] = userHash;
            var marketplaceListings = await _context.MealPlanListings
                .Include(x => x.MealPlan)
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            var recipeIds = marketplaceListings
                .SelectMany(l => ExtractRecipeIdsFromMealPlanJson(l.MealPlan?.MealPlan))
                .Distinct()
                .ToList();
            var recipeMediaMap = await BuildRecipeMediaMapAsync(recipeIds);

            // Build nutrition totals per listing
            var nutritionMap = new Dictionary<int, NutritionTotals>();
            foreach (var listing in marketplaceListings)
            {
                var lRecipeIds = ExtractRecipeIdsFromMealPlanJson(listing.MealPlan?.MealPlan)
                    .Where(id => id > 0).Distinct().ToList();
                if (lRecipeIds.Any())
                    nutritionMap[listing.Id] = await BuildNutritionTotalsAsync(lRecipeIds);
            }

            ViewData["MarketplaceListings"] = marketplaceListings;
            ViewData["CreatorShopCards"] = marketplaceListings.Select(listing =>
            {
                var listingRecipeIds = ExtractRecipeIdsFromMealPlanJson(listing.MealPlan?.MealPlan);
                var heroImages = listingRecipeIds
                    .Where(recipeMediaMap.ContainsKey)
                    .Select(id => recipeMediaMap[id])
                    .Where(media => !string.IsNullOrWhiteSpace(media.ImageUrl))
                    .ToList();

                nutritionMap.TryGetValue(listing.Id, out var nutrition);
                var desc = listing.Description ?? string.Empty;

                return new PlanCardViewModel
                {
                    ListingId = listing.Id,
                    Title = listing.Title,
                    TitleJsSafe = (listing.Title ?? string.Empty).Replace("'", "\\'"),
                    Description = listing.Description,
                    CreatorName = listing.SellerName,
                    CreatorHash = (listing.SellerHash ?? string.Empty).ToLowerInvariant(),
                    SellerWalletAddress = listing.SellerWalletAddress,
                    DayCount = listing.DayCount,
                    RecipeCount = listing.RecipeCount,
                    CreatedDateLabel = listing.CreatedAt.ToString("dd.MM.yy"),
                    PriceWld = listing.Price,
                    CaloriesKcal = nutrition?.Calories,
                    ProteinGrams = nutrition?.Protein,
                    FatGrams = nutrition?.Fat,
                    CarbsGrams = nutrition?.Carbohydrates,
                    Rating = listing.SoldCount > 0 ? 4.8m : 4.6m,
                    SoldCount = listing.SoldCount,
                    ActivePlannerCount = Math.Max(3, (listing.SoldCount % 17) + 3),
                    IsLowCarb = desc.Contains("low carb", StringComparison.OrdinalIgnoreCase),
                    IsDietFriendly = desc.Contains("diet", StringComparison.OrdinalIgnoreCase)
                        || desc.Contains("diät", StringComparison.OrdinalIgnoreCase),
                    Tags = ExtractTags(desc, nutrition),
                    HeroSlides = heroImages.Select(x => new PlanCardHeroSlideViewModel { ImageUrl = x.ImageUrl, RecipeTitle = x.RecipeTitle }).ToList(),
                    HeroImageUrls = heroImages.Select(x => x.ImageUrl).ToList(),
                    HeroImageUrl = heroImages.Select(x => x.ImageUrl).FirstOrDefault()
                };
            }).ToList();

            ViewData["WalletWLD"] = HttpContext.Session.GetString(SessionWalletWLD) ?? "";
            ViewData["WalletUSDT"] = HttpContext.Session.GetString(SessionWalletUSDT) ?? "";
            ViewData["WorldChainId"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:ChainId"] ?? "480";
            ViewData["WorldChainWldToken"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:WldTokenAddress"] ?? "";
            ViewData["WorldChainUsdtToken"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:UsdtTokenAddress"] ?? "";
            ViewData["WorldChainMarketplace"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:MarketplaceContractAddress"] ?? "";
            ViewData["WorldChainTestMode"] = bool.TryParse(HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:TestMode"], out var testMode) && testMode;

            return View(model);
        }

        private static List<int> ExtractRecipeIdsFromMealPlanJson(string? mealPlanJson)
        {
            if (string.IsNullOrWhiteSpace(mealPlanJson)) return new List<int>();
            try
            {
                var indexIds = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(mealPlanJson);
                return indexIds?.Values.SelectMany(x => x).ToList() ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private async Task<Dictionary<int, (string ImageUrl, string RecipeTitle)>> BuildRecipeMediaMapAsync(List<int> recipeIds)
        {
            var result = new Dictionary<int, (string ImageUrl, string RecipeTitle)>();
            if (!recipeIds.Any()) return result;

            var postingMedia = await _context.WorldUserPosting
                .AsNoTracking()
                .Where(p => p.Recipe != null && recipeIds.Contains(p.Recipe.Id))
                .Select(p => new
                {
                    RecipeId = p.Recipe.Id,
                    p.ThumbnailUrl,
                    p.Source,
                    RecipeTitle = p.Recipe.Title,
                    p.CreationTime
                })
                .OrderByDescending(p => p.CreationTime)
                .ToListAsync();

            foreach (var group in postingMedia.GroupBy(x => x.RecipeId))
            {
                var preferredImage = group
                    .Select(x => x.ThumbnailUrl)
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

                preferredImage ??= group
                    .Select(x => x.Source)
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && !IsVideoPath(path));

                // Do NOT fall back to a video URL as an <img> src (breaks the grid).
                // If no thumbnail exists, we fall back to RecipeBaseData.Images below.

                if (string.IsNullOrWhiteSpace(preferredImage))
                    continue;

                var title = group.Select(x => x.RecipeTitle).FirstOrDefault() ?? string.Empty;
                result[group.Key] = (NormalizeRecipeImagePath(preferredImage), title);
            }

            var missingBaseRecipeIds = recipeIds.Where(id => !result.ContainsKey(id)).ToList();

            var baseRecipes = await _context.RecipeBaseData
                .Include(r => r.Images)
                .AsNoTracking()
                .Where(r => missingBaseRecipeIds.Contains(r.Id))
                .ToListAsync();

            foreach (var recipe in baseRecipes)
            {
                var image = recipe.Images?.FirstOrDefault()?.Image;
                if (string.IsNullOrWhiteSpace(image)) continue;
                result[recipe.Id] = (NormalizeRecipeImagePath(image), recipe.Title ?? string.Empty);
            }

            var missingIds = recipeIds.Where(id => !result.ContainsKey(id)).ToList();
            if (missingIds.Any())
            {
                var classicRecipes = await _context.Recipes
                    .AsNoTracking()
                    .Where(r => missingIds.Contains(r.Id) && r.ImagePath != null)
                    .ToListAsync();

                foreach (var recipe in classicRecipes)
                {
                    if (string.IsNullOrWhiteSpace(recipe.ImagePath)) continue;
                    result[recipe.Id] = (NormalizeRecipeImagePath(recipe.ImagePath), recipe.Name ?? string.Empty);
                }
            }

            return result;
        }


        private string NormalizeRecipeImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return string.Empty;
            if (Uri.IsWellFormedUriString(imagePath, UriKind.Absolute)) return ChangePath(imagePath);
            return ChangePath(FrontendFunctions.GetSmallImagePath(imagePath));
        }

        private static bool IsVideoPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var lower = path.ToLowerInvariant();
            return lower.Contains(".mp4") || lower.Contains(".mov") || lower.Contains(".webm") || lower.Contains(".m3u8");
        }

        private static string? ResolveCanonicalCategory(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var normalizedInput = NormalizeCategory(input);
            return CategoryAliasLookup.TryGetValue(normalizedInput, out var canonicalCategory)
                ? canonicalCategory
                : null;
        }

        private static string NormalizeCategory(string value)
        {
            var normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace("-", string.Empty).Replace(" ", string.Empty);

            var withoutDiacritics = normalized
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray();

            return new string(withoutDiacritics).Normalize(NormalizationForm.FormC);
        }


        private string ChangePath(string path)
        {

            if (string.IsNullOrEmpty(path)) return path;

          
            // TODO: Secret noch entfernen — CDN-Domains in Konfiguration auslagern
            string oldDomain = "blobdelikatessendrehbuch.blob.core.windows.net";
            string newCdnDomain = "DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net";


            if (path.Contains(oldDomain))
            {
                return path.Replace(oldDomain, newCdnDomain);
            }

            return path;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike([FromForm] string userHash, int recipeId)
        {
            userHash = ResolveUserHash(userHash);
            try
            {
                await AddOrRemoveLike(userHash, recipeId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle like for recipe {RecipeId}.", recipeId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ein unerwarteter Fehler ist aufgetreten.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetComments(int postingId, string sort = "top", CancellationToken cancellationToken = default)
        {
            if (postingId <= 0)
                return BadRequest(new { message = "postingId fehlt." });

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            var currentUserHash = ResolveUserHash(string.Empty);
            var canWrite = await CanWriteCommentsAsync(currentUserHash, cancellationToken);
            var canReactOrReport = await CanReactOrReportCommentsAsync(currentUserHash, cancellationToken);
            var canModerate = await CanModerateCommentsAsync(currentUserHash, cancellationToken);

            var creatorUserHash = await _context.WorldUserPosting
                .AsNoTracking()
                .Where(p => p.Id == postingId)
                .Select(p => p.CreatorId)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var comments = await _context.WorldUserComments
                .AsNoTracking()
                .Where(x => x.WorldUserPostingId == postingId && !x.IsDeleted)
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new CommentListItem
                {
                    Id = x.Id,
                    ParentCommentId = x.ParentCommentId,
                    UserHash = x.UserHash,
                    UserName = x.UserName,
                    VerificationLevel = x.VerificationLevel,
                    Text = x.CommentText,
                    CreatedAtUtc = x.CreatedAtUtc,
                    IsOwn = !string.IsNullOrWhiteSpace(currentUserHash) && x.UserHash == currentUserHash,
                    IsPinned = x.IsPinned
                })
                .ToListAsync(cancellationToken);

            var commentIds = comments.Select(x => x.Id).ToList();
            var reactionStats = await GetCommentReactionStatsAsync(commentIds, currentUserHash, cancellationToken);
            var reportedIds = await GetReportedCommentIdsAsync(commentIds, currentUserHash, cancellationToken);
            var reportCounts = await GetCommentReportCountsAsync(commentIds, cancellationToken);

            var visibleComments = comments
                .Where(item =>
                {
                    var reportCount = reportCounts.TryGetValue(item.Id, out var rc) ? rc : 0;
                    var autoHidden = reportCount >= CommentAutoHideReportThreshold;
                    return !autoHidden || item.IsOwn || canModerate;
                })
                .ToList();

            var replyCounts = visibleComments
                .Where(x => x.ParentCommentId.HasValue)
                .GroupBy(x => x.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var normalizedSort = NormalizeCommentSort(sort);

            object MapComment(CommentListItem item)
            {
                var reactionSummary = reactionStats.TryGetValue(item.Id, out var stats)
                    ? stats
                    : new CommentReactionSummary();
                var reportCount = reportCounts.TryGetValue(item.Id, out var resolvedReportCount)
                    ? resolvedReportCount
                    : 0;

                return new
                {
                    id = item.Id,
                    parentCommentId = item.ParentCommentId,
                    userHash = item.UserHash,
                    userName = item.UserName,
                    verificationLevel = item.VerificationLevel,
                    text = item.Text,
                    createdAtUtc = item.CreatedAtUtc,
                    isOwn = item.IsOwn,
                    isCreator = !string.IsNullOrWhiteSpace(creatorUserHash) && item.UserHash == creatorUserHash,
                    isPinned = item.IsPinned,
                    canDelete = item.IsOwn || IsCommentModerator(currentUserHash),
                    likeCount = reactionSummary.LikeCount,
                    dislikeCount = reactionSummary.DislikeCount,
                    myReaction = reactionSummary.MyReaction,
                    hasReported = reportedIds.Contains(item.Id),
                    reportCount,
                    isAutoHidden = reportCount >= CommentAutoHideReportThreshold
                };
            };

            var topLevelComments = visibleComments
                .Where(x => !x.ParentCommentId.HasValue)
                .Select(x => new
                {
                    Raw = x,
                    Reaction = reactionStats.TryGetValue(x.Id, out var rs) ? rs : new CommentReactionSummary(),
                    ReplyCount = replyCounts.TryGetValue(x.Id, out var rc) ? rc : 0
                });

            var orderedTopLevel = normalizedSort == "new"
                ? topLevelComments
                    .OrderByDescending(x => x.Raw.IsPinned)
                    .ThenByDescending(x => x.Raw.CreatedAtUtc).ToList()
                : topLevelComments
                    .OrderByDescending(x => x.Raw.IsPinned)
                    .ThenByDescending(x => x.Reaction.LikeCount - x.Reaction.DislikeCount)
                    .ThenByDescending(x => x.Reaction.LikeCount)
                    .ThenByDescending(x => x.ReplyCount)
                    .ThenByDescending(x => x.Raw.CreatedAtUtc)
                    .ToList();

            var isCurrentUserCreator = !string.IsNullOrWhiteSpace(currentUserHash) && currentUserHash == creatorUserHash;

            return Json(new
            {
                success = true,
                postingId,
                canWrite,
                canReactOrReport,
                canModerate,
                canPin = isCurrentUserCreator,
                isLoggedIn = !string.IsNullOrWhiteSpace(currentUserHash),
                sort = normalizedSort,
                commentCount = visibleComments.Count,
                items = orderedTopLevel.Select(parent => new
                {
                    id = parent.Raw.Id,
                    parentCommentId = parent.Raw.ParentCommentId,
                    userHash = parent.Raw.UserHash,
                    userName = parent.Raw.UserName,
                    verificationLevel = parent.Raw.VerificationLevel,
                    text = parent.Raw.Text,
                    createdAtUtc = parent.Raw.CreatedAtUtc,
                    isOwn = parent.Raw.IsOwn,
                    isCreator = !string.IsNullOrWhiteSpace(creatorUserHash) && parent.Raw.UserHash == creatorUserHash,
                    isPinned = parent.Raw.IsPinned,
                    canDelete = parent.Raw.IsOwn || IsCommentModerator(currentUserHash),
                    likeCount = parent.Reaction.LikeCount,
                    dislikeCount = parent.Reaction.DislikeCount,
                    myReaction = parent.Reaction.MyReaction,
                    hasReported = reportedIds.Contains(parent.Raw.Id),
                    reportCount = reportCounts.TryGetValue(parent.Raw.Id, out var parentReportCount) ? parentReportCount : 0,
                    isAutoHidden = reportCounts.TryGetValue(parent.Raw.Id, out var parentAutoHideCount) && parentAutoHideCount >= CommentAutoHideReportThreshold,
                    replyCount = parent.ReplyCount,
                    replies = visibleComments
                        .Where(x => x.ParentCommentId == parent.Raw.Id)
                        .OrderBy(x => x.CreatedAtUtc)
                        .Select(x => MapComment(x))
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromForm] string userHash, [FromForm] int postingId, [FromForm] string text, [FromForm] int? parentCommentId, CancellationToken cancellationToken)
        {
            userHash = ResolveUserHash(userHash);
            if (postingId <= 0)
                return BadRequest(new { message = "Posting fehlt." });

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            if (!await CanWriteCommentsAsync(userHash, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur orb-verifizierte Nutzer koennen Kommentare schreiben." });

            var normalizedText = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedText))
                return BadRequest(new { message = "Kommentar ist leer." });

            if (normalizedText.Length > 500)
                return BadRequest(new { message = "Kommentar ist zu lang." });

            var posting = await _context.WorldUserPosting
                .AsNoTracking()
                .Include(x => x.Recipe)
                .FirstOrDefaultAsync(x => x.Id == postingId && !x.IsOffline, cancellationToken);
            if (posting == null)
                return NotFound(new { message = "Posting wurde nicht gefunden." });

            WorldUserComment? parentComment = null;
            int? normalizedParentCommentId = null;
            if (parentCommentId.HasValue && parentCommentId.Value > 0)
            {
                parentComment = await _context.WorldUserComments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == parentCommentId.Value && x.WorldUserPostingId == postingId && !x.IsDeleted, cancellationToken);

                if (parentComment == null)
                    return BadRequest(new { message = "Antwortziel wurde nicht gefunden." });

                normalizedParentCommentId = parentComment.ParentCommentId ?? parentComment.Id;
            }

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash, cancellationToken);
            if (user == null)
                return BadRequest(new { message = "User wurde nicht gefunden." });

            var entity = new WorldUserComment
            {
                WorldUserPostingId = postingId,
                ParentCommentId = normalizedParentCommentId,
                UserHash = userHash,
                UserName = string.IsNullOrWhiteSpace(user.UserName) ? "User" : user.UserName.Trim(),
                VerificationLevel = user.IsVerified ?? string.Empty,
                CommentText = normalizedText,
                CreatedAtUtc = DateTime.UtcNow,
                IsDeleted = false
            };

            await _context.WorldUserComments.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await TryAddCommentNotificationsAsync(posting, entity, parentComment, cancellationToken);

            var commentCount = await _context.WorldUserComments
                .AsNoTracking()
                .CountAsync(x => x.WorldUserPostingId == postingId && !x.IsDeleted, cancellationToken);

            return Json(new
            {
                success = true,
                postingId,
                commentCount,
                item = new
                {
                    id = entity.Id,
                    userHash = entity.UserHash,
                    userName = entity.UserName,
                    verificationLevel = entity.VerificationLevel,
                    text = entity.CommentText,
                    createdAtUtc = entity.CreatedAtUtc,
                    isOwn = true,
                    parentCommentId = entity.ParentCommentId
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCommentReaction([FromForm] string userHash, [FromForm] int commentId, [FromForm] string reaction, CancellationToken cancellationToken)
        {
            userHash = ResolveUserHash(userHash);
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            if (!await CanReactOrReportCommentsAsync(userHash, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur angemeldete Nutzer koennen auf Kommentare reagieren." });

            var normalizedReaction = (reaction ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedReaction != "like" && normalizedReaction != "dislike")
                return BadRequest(new { message = "Reaktion ist ungueltig." });

            var comment = await _context.WorldUserComments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            var entity = await _context.WorldUserCommentReactions
                .FirstOrDefaultAsync(x => x.WorldUserCommentId == commentId && x.UserHash == userHash, cancellationToken);

            var wantsLike = normalizedReaction == "like";
            var isNewLike = false;
            if (entity == null)
            {
                entity = new WorldUserCommentReaction
                {
                    WorldUserCommentId = commentId,
                    UserHash = userHash,
                    IsLike = wantsLike,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.WorldUserCommentReactions.AddAsync(entity, cancellationToken);
                isNewLike = wantsLike;
            }
            else if (entity.IsLike == wantsLike)
            {
                _context.WorldUserCommentReactions.Remove(entity);
                normalizedReaction = string.Empty;
            }
            else
            {
                entity.IsLike = wantsLike;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                isNewLike = wantsLike;
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Create notification for comment owner if this was a new like
            if (isNewLike)
            {
                var user = await _context.WorldAppUser
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserHash == userHash, cancellationToken);
                var likerName = user?.UserName ?? "Jemand";
                await TryAddCommentLikeNotificationAsync(userHash, likerName, commentId, cancellationToken);
            }

            var summary = await GetCommentReactionSummaryAsync(commentId, userHash, cancellationToken);
            return Json(new
            {
                success = true,
                commentId,
                likeCount = summary.LikeCount,
                dislikeCount = summary.DislikeCount,
                myReaction = summary.MyReaction
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportComment([FromForm] string userHash, [FromForm] int commentId, [FromForm] string reason, CancellationToken cancellationToken)
        {
            userHash = ResolveUserHash(userHash);
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            if (!await CanReactOrReportCommentsAsync(userHash, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur angemeldete Nutzer koennen Kommentare melden." });

            var comment = await _context.WorldUserComments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            if (string.Equals(comment.UserHash, userHash, StringComparison.Ordinal))
                return BadRequest(new { message = "Eigene Kommentare musst du nicht melden." });

            var existingReport = await _context.WorldUserCommentReports
                .AsNoTracking()
                .AnyAsync(x => x.WorldUserCommentId == commentId && x.UserHash == userHash, cancellationToken);
            if (existingReport)
                return Json(new { success = true, commentId, hasReported = true });

            var normalizedReason = (reason ?? string.Empty).Trim();
            if (normalizedReason.Length > 500)
                normalizedReason = normalizedReason[..500];

            var report = new WorldUserCommentReport
            {
                WorldUserCommentId = commentId,
                UserHash = userHash,
                Reason = normalizedReason,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.WorldUserCommentReports.AddAsync(report, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, commentId, hasReported = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment([FromForm] string userHash, [FromForm] int commentId, CancellationToken cancellationToken)
        {
            userHash = ResolveUserHash(userHash);
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            var comment = await _context.WorldUserComments
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            var canModerate = await CanModerateCommentsAsync(userHash, cancellationToken);
            if (!canModerate && !string.Equals(comment.UserHash, userHash, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Du darfst diesen Kommentar nicht loeschen." });

            comment.IsDeleted = true;
            comment.CommentText = string.Empty;

            var childComments = await _context.WorldUserComments
                .Where(x => x.ParentCommentId == commentId && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var child in childComments)
            {
                child.IsDeleted = true;
                child.CommentText = string.Empty;
            }

            var deletedCommentIds = childComments.Select(x => x.Id).Append(commentId).ToList();
            var reports = await _context.WorldUserCommentReports
                .Where(x => deletedCommentIds.Contains(x.WorldUserCommentId))
                .ToListAsync(cancellationToken);
            if (reports.Count > 0)
                _context.WorldUserCommentReports.RemoveRange(reports);

            await _context.SaveChangesAsync(cancellationToken);

            var commentCount = await _context.WorldUserComments
                .AsNoTracking()
                .CountAsync(x => x.WorldUserPostingId == comment.WorldUserPostingId && !x.IsDeleted, cancellationToken);

            return Json(new
            {
                success = true,
                commentId,
                postingId = comment.WorldUserPostingId,
                commentCount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PinComment([FromForm] string userHash, [FromForm] int commentId, CancellationToken cancellationToken)
        {
            userHash = ResolveUserHash(userHash);
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            var comment = await _context.WorldUserComments
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            var posting = await _context.WorldUserPosting
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == comment.WorldUserPostingId, cancellationToken);
            if (posting == null || !string.Equals(posting.CreatorId, userHash, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur der Creator kann Kommentare anheften." });

            var currentlyPinned = await _context.WorldUserComments
                .Where(x => x.WorldUserPostingId == comment.WorldUserPostingId && x.IsPinned && x.Id != commentId)
                .ToListAsync(cancellationToken);
            foreach (var pinned in currentlyPinned)
                pinned.IsPinned = false;

            comment.IsPinned = !comment.IsPinned;
            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, commentId, isPinned = comment.IsPinned });
        }

        [HttpGet]
        public async Task<IActionResult> CommentModeration(string userHash = "", CancellationToken cancellationToken = default)
        {
            userHash = ResolveUserHash(userHash);
            if (!await CanModerateCommentsAsync(userHash, cancellationToken))
                return Forbid();

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            var reportRows = await _context.WorldUserCommentReports
                .AsNoTracking()
                .Join(_context.WorldUserComments.AsNoTracking(),
                    report => report.WorldUserCommentId,
                    comment => comment.Id,
                    (report, comment) => new { report, comment })
                .Where(x => !x.comment.IsDeleted)
                .OrderByDescending(x => x.report.CreatedAtUtc)
                .Select(x => new
                {
                    x.comment.Id,
                    x.comment.WorldUserPostingId,
                    x.comment.CommentText,
                    x.comment.UserName,
                    x.comment.UserHash,
                    x.comment.VerificationLevel,
                    x.comment.CreatedAtUtc,
                    ReportReason = x.report.Reason,
                    ReportCreatedAtUtc = x.report.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

            var items = reportRows
                .GroupBy(x => new
                {
                    x.Id,
                    x.WorldUserPostingId,
                    x.CommentText,
                    x.UserName,
                    x.UserHash,
                    x.VerificationLevel,
                    x.CreatedAtUtc
                })
                .Select(g => new CommentModerationItemViewModel
                {
                    CommentId = g.Key.Id,
                    PostingId = g.Key.WorldUserPostingId,
                    CommentText = g.Key.CommentText,
                    CommentUserName = g.Key.UserName,
                    CommentUserHash = g.Key.UserHash,
                    VerificationLevel = g.Key.VerificationLevel,
                    CommentCreatedAtUtc = g.Key.CreatedAtUtc,
                    ReportCount = g.Count(),
                    LatestReportAtUtc = g.Max(x => x.ReportCreatedAtUtc),
                    Reasons = g.GroupBy(x => string.IsNullOrWhiteSpace(x.ReportReason) ? "Ohne Grund" : x.ReportReason.Trim())
                        .Select(reasonGroup => new CommentReportReasonViewModel
                        {
                            Reason = reasonGroup.Key,
                            Count = reasonGroup.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList()
                })
                .OrderByDescending(x => x.LatestReportAtUtc)
                .ToList();

            var model = new CommentModerationViewModel
            {
                UserHash = userHash,
                OpenReportCount = reportRows.Count,
                Items = items
            };

            return View("~/Areas/WorldMiniApp/Views/Feed/CommentModeration.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveCommentReport([FromForm] string userHash, [FromForm] int commentId, [FromForm] string actionType, CancellationToken cancellationToken)
        {
            userHash = ResolveUserHash(userHash);
            if (!await CanModerateCommentsAsync(userHash, cancellationToken))
                return Forbid();

            if (commentId <= 0)
                return BadRequest();

            await EnsureWorldUserCommentsSchemaAsync(cancellationToken);

            var normalizedAction = (actionType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedAction != "dismiss" && normalizedAction != "delete")
                return BadRequest();

            var reports = await _context.WorldUserCommentReports
                .Where(x => x.WorldUserCommentId == commentId)
                .ToListAsync(cancellationToken);

            if (reports.Count > 0)
                _context.WorldUserCommentReports.RemoveRange(reports);

            if (normalizedAction == "delete")
            {
                var comment = await _context.WorldUserComments
                    .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);

                if (comment != null)
                {
                    comment.IsDeleted = true;
                    comment.CommentText = string.Empty;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(CommentModeration), new { userHash });
        }

        private async Task AddOrRemoveLike(string userHash, int recipeId)
        {
            var like = await _context.WorldUserLike
                           .FirstOrDefaultAsync(x => x.WorldAppUser.UserHash == userHash && x.Recipe.Id == recipeId);
            var recipe = await _context.RecipeBaseData.FirstOrDefaultAsync(x => x.Id == recipeId);
            if (recipe == null)
            {
                return;
            }

            if (like != null)
            {
                _context.WorldUserLike.Remove(like);
                recipe.LikeCount = Math.Max(0, recipe.LikeCount - 1);
            }
            else
            {
                var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == userHash);


                if (user != null)
                {
                    WorldUserLike newLike = new WorldUserLike()
                    {
                        Recipe = recipe,
                        WorldAppUser = user
                    };
                    recipe.LikeCount++;
                    await _context.WorldUserLike.AddAsync(newLike);

                    // Create notification for posting owner
                    await TryAddLikeNotificationAsync(userHash, user.UserName ?? "Jemand", recipeId, CancellationToken.None);
                }
            }
            await _context.SaveChangesAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow([FromForm] string userHash, [FromForm] string creatorId)
        {
            userHash = ResolveUserHash(userHash);

            var currentUser = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            var creator = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == creatorId);

            if (currentUser == null || creator == null) return BadRequest("User oder Creator nicht gefunden");
            if (currentUser.Id == creator.Id) return BadRequest("Du kannst dir nicht selbst folgen");


            var existingAbo = await _context.WorldUserAbo
                .FirstOrDefaultAsync(x => x.WorldUser.Id == currentUser.Id && x.Creator.Id == creator.Id);

            if (existingAbo != null)
            {
                _context.WorldUserAbo.Remove(existingAbo);
            }
            else
            {
                var newAbo = new WorldUserAbo
                {
                    WorldUser = currentUser,
                    Creator = creator
                };
                await _context.WorldUserAbo.AddAsync(newAbo);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }


        public async Task<IActionResult> MyProfile(string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrEmpty(userHash)) return RedirectToAction("Index");

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (user == null) return NotFound();

            // --- BASIS DATEN (F�r alle) ---

            // 1. Likes laden
            var likes = _context.WorldUserLike.Where(x => x.WorldAppUser.UserHash == userHash).Select(x => x.Recipe.Id);

            var likedRecipes = await _context.WorldUserPosting
                .Where(x => likes.Contains(x.Recipe.Id))
                .Include(x => x.Recipe).ThenInclude(r => r.Images)
                .ToListAsync();

            // Ensure profile grids never try to render a video URL as an <img> src.
            void EnsurePostingHasGridThumbnail(WorldUserPosting post)
            {
                if (post == null) return;

                if (!string.IsNullOrWhiteSpace(post.ThumbnailUrl))
                {
                    post.ThumbnailUrl = ChangePath(post.ThumbnailUrl);
                    return;
                }

                var source = post.Source ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(source) && !IsVideoPath(source))
                {
                    post.ThumbnailUrl = ChangePath(source);
                    return;
                }

                var img = post.Recipe?.Images?.FirstOrDefault()?.Image;
                if (!string.IsNullOrWhiteSpace(img))
                {
                    post.ThumbnailUrl = NormalizeRecipeImagePath(img);
                }
            }

            foreach (var post in likedRecipes)
            {
                EnsurePostingHasGridThumbnail(post);
            }
            
            // AI variants created by this user (one card per variant; users can have multiple highprotein versions).
            var rawAiVariants = await _context.RecipeAiBaseRecipes
                .AsNoTracking()
                .Where(x => x.CreatedByUserHash == userHash)
                .Select(x => new { x.Id, x.BaseRecipeId, x.VariantType, x.Title, x.UpdatedAtUtc })
                .ToListAsync();

            var aiRecipeIds = rawAiVariants.Select(x => x.BaseRecipeId).Distinct().ToList();
            var aiMediaMap = await BuildRecipeMediaMapAsync(aiRecipeIds);

            var aiEditedCards = rawAiVariants
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Take(80)
                .Select(v =>
                {
                    var media = aiMediaMap.TryGetValue(v.BaseRecipeId, out var m) ? m : (ImageUrl: string.Empty, RecipeTitle: string.Empty);
                    var baseTitle = string.IsNullOrWhiteSpace(media.RecipeTitle) ? "Rezept" : media.RecipeTitle;
                    var variantType = (v.VariantType ?? string.Empty).Trim().ToLowerInvariant();
                    var variantLabel = variantType switch
                    {
                        "vegan" => "Vegan",
                        "mealprep" => "Meal Prep",
                        "lowcarb" => "Low Carb",
                        "highprotein" => "Mehr Protein",
                        _ => "AI"
                    };

                    return new WorldAiEditedRecipeCard
                    {
                        AiVariantId = v.Id,
                        BaseRecipeId = v.BaseRecipeId,
                        BaseTitle = baseTitle,
                        VariantTitle = string.IsNullOrWhiteSpace(v.Title) ? baseTitle : v.Title,
                        ImageUrl = media.ImageUrl,
                        VariantType = variantType,
                        VariantLabel = variantLabel,
                        UpdatedAtUtc = v.UpdatedAtUtc
                    };
                })
                .ToList();

            // 2. Abos laden (Wen verfolge ich?)
            var following = await _context.WorldUserAbo
                .Where(a => a.WorldUser.Id == user.Id)
                .Include(a => a.Creator)
                .Select(a => a.Creator)
                .ToListAsync();

            var mealPlans = await _context.WorldUserMealPlan
                .Where(p => p.UserHash == userHash)
                .OrderByDescending(p => p.CreationTime)
                .ToListAsync();

            // --- ORB / CREATOR DATEN (Nur wenn verifiziert) ---

            var myVideos = new List<WorldUserPosting>();
            int followerCount = 0;

            // Pr�fung auf "orb"
            // TODO: Secret noch entfernen — hardcodierten SuperUserHash durch Konfiguration ersetzen
            if (user.IsVerified == "orb" || user.UserHash == "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839")
            {
                // 3. Eigene Videos laden
                myVideos = await _context.WorldUserPosting
                    .Where(p => p.CreatorId == user.UserHash) // Oder User.Id, je nach deiner DB
                    .Include(p => p.Recipe).ThenInclude(r => r.Images)
                    .OrderByDescending(p => p.CreationTime)
                    .ToListAsync();

                foreach (var post in myVideos)
                {
                    EnsurePostingHasGridThumbnail(post);
                }

                // 4. Follower z�hlen (Wer folgt mir?)
                followerCount = await _context.WorldUserAbo
                    .CountAsync(a => a.Creator.Id == user.Id);
            }

            var purchases = await _coinService.GetPurchasesByBuyerAsync(userHash);
            var purchasedMealPlanIds = purchases
                .Select(p => p.CreatedMealPlanId)
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet();

            var createdMealPlans = mealPlans
                .Where(p => !purchasedMealPlanIds.Contains(p.Id))
                .ToList();

            var purchasedMealPlans = mealPlans
                .Where(p => purchasedMealPlanIds.Contains(p.Id))
                .ToList();

            var model = new UserProfileViewModel
            {
                User = user,
                LikedRecipes = likedRecipes,
                Following = following,
                MealPlans = mealPlans,
                CreatedMealPlans = createdMealPlans,
                PurchasedMealPlans = purchasedMealPlans,
                Purchases = purchases,
                MyVideos = myVideos,
                FollowerCount = followerCount,
                AiEditedRecipes = aiEditedCards
            };

            ViewData["IsWorldMiniAppAdmin"] = await CanModerateCommentsAsync(userHash);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string userHash, string userName)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (user == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                user.UserName = userName.Trim();
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(MyProfile), new { userHash });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPostingOffline([FromForm] int postingId, [FromForm] string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Forbid();
            }

            var posting = await _context.WorldUserPosting.FirstOrDefaultAsync(p => p.Id == postingId);
            if (posting == null)
            {
                return NotFound();
            }

            if (!string.Equals(posting.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            posting.IsOffline = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserHash} set posting {PostingId} offline.", userHash, postingId);

            return RedirectToAction(nameof(MyProfile), new { userHash });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPostingOnline([FromForm] int postingId, [FromForm] string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Forbid();
            }

            var posting = await _context.WorldUserPosting.FirstOrDefaultAsync(p => p.Id == postingId);
            if (posting == null)
            {
                return NotFound();
            }

            if (!string.Equals(posting.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            posting.IsOffline = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserHash} set posting {PostingId} online.", userHash, postingId);

            return RedirectToAction(nameof(MyProfile), new { userHash });
        }

        [HttpGet]
        public async Task<IActionResult> GetLikedRecipes()
        {
            var userHash = ResolveUserHash("");
            if (string.IsNullOrEmpty(userHash))
                return Json(Array.Empty<object>());

            var liked = await _context.WorldUserLike
                .AsNoTracking()
                .Where(l => l.WorldAppUser.UserHash == userHash)
                .Select(l => new
                {
                    recipeId = l.Recipe.Id,
                    title = l.Recipe.Title
                })
                .ToListAsync();

            if (!liked.Any())
                return Json(Array.Empty<object>());

            var recipeIds = liked.Select(l => l.recipeId).ToList();
            var mediaMap = await BuildRecipeMediaMapAsync(recipeIds);

            var result = liked.Select(l => new
            {
                l.recipeId,
                l.title,
                img = mediaMap.TryGetValue(l.recipeId, out var m) ? m.ImageUrl : ""
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> CreatorPostings(string creatorHash)
        {
            if (string.IsNullOrWhiteSpace(creatorHash))
                return BadRequest("creatorHash is required");

            await EnsureWorldUserCommentsSchemaAsync();

            var currentUserHash = ResolveUserHash("");
            var likedIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(currentUserHash))
            {
                likedIds = (await _context.WorldUserLike
                    .AsNoTracking()
                    .Where(l => l.WorldAppUser.UserHash == currentUserHash)
                    .Select(l => l.Recipe.Id)
                    .ToListAsync()).ToHashSet();
            }

            var postings = await _context.WorldUserPosting
                .AsNoTracking()
                .Include(p => p.Recipe)
                .Where(p => p.CreatorId == creatorHash && !p.IsOffline)
                .OrderByDescending(p => p.Id)
                .Take(20)
                .Select(p => new
                {
                    id = p.Id,
                    postingId = p.Id,
                    title = p.Recipe.Title,
                    source = p.Source,
                    thumbnailUrl = p.ThumbnailUrl,
                    creatorName = p.CreatorName,
                    creatorId = p.CreatorId,
                    prepTime = p.Recipe.PreparationTime,
                    recipeId = p.Recipe.Id,
                    category = p.Recipe.Category
                })
                .ToListAsync();

            // Like-Counts für Creator-Postings
            var creatorRecipeIds = postings.Select(p => p.recipeId).Distinct().ToList();
            var creatorLikeCounts = creatorRecipeIds.Count > 0
                ? await _context.WorldUserLike
                    .AsNoTracking()
                    .Where(l => creatorRecipeIds.Contains(l.Recipe.Id))
                    .GroupBy(l => l.Recipe.Id)
                    .Select(g => new { RecipeId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.RecipeId, x => x.Count)
                : new Dictionary<int, int>();

            var creatorPostingIds = postings.Select(p => p.postingId).Distinct().ToList();
            var creatorCommentCounts = await GetCommentCountsForPostingsAsync(creatorPostingIds);

            var result = postings.Select(p => new
            {
                p.id,
                p.postingId,
                p.title,
                source = ChangePath(p.source),
                thumbnailUrl = ChangePath(p.thumbnailUrl),
                p.creatorName,
                p.creatorId,
                p.prepTime,
                p.recipeId,
                p.category,
                isLiked = likedIds.Contains(p.recipeId),
                likeCount = creatorLikeCounts.TryGetValue(p.recipeId, out var clc) ? clc : 0,
                commentCount = creatorCommentCounts.TryGetValue(p.postingId, out var ccc) ? ccc : 0
            });

            return Json(result);
        }

        // ── Notification Endpoints ──

        [HttpGet]
        public async Task<IActionResult> GetNotifications(string? filter = null, int limit = 50, CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, message = "Nicht angemeldet." });

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var query = _context.WorldUserNotifications
                .AsNoTracking()
                .Where(x => x.UserHash == userHash);

            if (filter == "unread")
                query = query.Where(x => !x.IsSeen);

            var notifications = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(Math.Min(limit, 100))
                .Select(x => new
                {
                    id = x.Id,
                    icon = x.Icon,
                    sender = x.Sender,
                    description = x.Description,
                    href = x.Href,
                    createdAtUtc = x.CreatedAtUtc,
                    isSeen = x.IsSeen,
                    seenAtUtc = x.SeenAtUtc
                })
                .ToListAsync(cancellationToken);

            return Json(new { success = true, notifications });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead([FromForm] int notificationId, CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, message = "Nicht angemeldet." });

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var notification = await _context.WorldUserNotifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserHash == userHash, cancellationToken);

            if (notification == null)
                return Json(new { success = false, message = "Benachrichtigung nicht gefunden." });

            if (!notification.IsSeen)
            {
                notification.IsSeen = true;
                notification.SeenAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead(CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, message = "Nicht angemeldet." });

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var unreadNotifications = await _context.WorldUserNotifications
                .Where(x => x.UserHash == userHash && !x.IsSeen)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var notification in unreadNotifications)
            {
                notification.IsSeen = true;
                notification.SeenAtUtc = now;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, count = unreadNotifications.Count });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount(CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { count = 0 });

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var count = await _context.WorldUserNotifications
                .AsNoTracking()
                .CountAsync(x => x.UserHash == userHash && !x.IsSeen, cancellationToken);

            return Json(new { count });
        }

        private string ResolveUserHash(string userHash)
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext, userHash);
        }

        private async Task<bool> CanWriteCommentsAsync(string userHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return false;

            if (IsCommentModerator(userHash))
                return true;

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash, cancellationToken);

            return string.Equals(user?.IsVerified, "orb", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanReactOrReportCommentsAsync(string userHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return false;

            return await _context.WorldAppUser
                .AsNoTracking()
                .AnyAsync(x => x.UserHash == userHash, cancellationToken);
        }

        private bool IsCommentModerator(string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

            return string.Equals(userHash, WorldMiniAppAdminHash, StringComparison.OrdinalIgnoreCase)
                || GetConfiguredCommentModeratorHashes().Contains(userHash, StringComparer.OrdinalIgnoreCase)
                || (User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin"));
        }

        private IReadOnlyList<string> GetConfiguredCommentModeratorHashes()
        {
            var values = _configuration
                .GetSection("WorldMiniApp:AdminUserHashes")
                .Get<string[]>();

            if (values != null && values.Length > 0)
            {
                return values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var raw = _configuration["WorldMiniApp:AdminUserHashes"];
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            return raw
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private Task<bool> CanModerateCommentsAsync(string userHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsCommentModerator(userHash));
        }

        private async Task<Dictionary<int, int>> GetCommentCountsForPostingsAsync(List<int> postingIds, CancellationToken cancellationToken = default)
        {
            if (postingIds == null || postingIds.Count == 0)
                return new Dictionary<int, int>();

            return await _context.WorldUserComments
                .AsNoTracking()
                .Where(x => postingIds.Contains(x.WorldUserPostingId) && !x.IsDeleted)
                .GroupBy(x => x.WorldUserPostingId)
                .Select(g => new { PostingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostingId, x => x.Count, cancellationToken);
        }

        private static string NormalizeCommentSort(string? sort)
        {
            return string.Equals(sort, "new", StringComparison.OrdinalIgnoreCase) ? "new" : "top";
        }

        private sealed class CommentReactionSummary
        {
            public int LikeCount { get; init; }
            public int DislikeCount { get; init; }
            public string MyReaction { get; init; } = string.Empty;
        }

        private sealed class CommentListItem
        {
            public int Id { get; init; }
            public int? ParentCommentId { get; init; }
            public string UserHash { get; init; } = string.Empty;
            public string UserName { get; init; } = string.Empty;
            public string VerificationLevel { get; init; } = string.Empty;
            public string Text { get; init; } = string.Empty;
            public DateTime CreatedAtUtc { get; init; }
            public bool IsOwn { get; init; }
            public bool IsPinned { get; init; }
        }

        private async Task<Dictionary<int, CommentReactionSummary>> GetCommentReactionStatsAsync(List<int> commentIds, string currentUserHash, CancellationToken cancellationToken = default)
        {
            if (commentIds == null || commentIds.Count == 0)
                return new Dictionary<int, CommentReactionSummary>();

            var grouped = await _context.WorldUserCommentReactions
                .AsNoTracking()
                .Where(x => commentIds.Contains(x.WorldUserCommentId))
                .GroupBy(x => x.WorldUserCommentId)
                .Select(g => new
                {
                    CommentId = g.Key,
                    LikeCount = g.Count(x => x.IsLike),
                    DislikeCount = g.Count(x => !x.IsLike)
                })
                .ToListAsync(cancellationToken);

            var summaries = grouped.ToDictionary(
                x => x.CommentId,
                x => new CommentReactionSummary
                {
                    LikeCount = x.LikeCount,
                    DislikeCount = x.DislikeCount
                });

            if (!string.IsNullOrWhiteSpace(currentUserHash))
            {
                var ownReactions = await _context.WorldUserCommentReactions
                    .AsNoTracking()
                    .Where(x => commentIds.Contains(x.WorldUserCommentId) && x.UserHash == currentUserHash)
                    .Select(x => new
                    {
                        x.WorldUserCommentId,
                        MyReaction = x.IsLike ? "like" : "dislike"
                    })
                    .ToListAsync(cancellationToken);

                foreach (var own in ownReactions)
                {
                    if (summaries.TryGetValue(own.WorldUserCommentId, out var existing))
                    {
                        summaries[own.WorldUserCommentId] = new CommentReactionSummary
                        {
                            LikeCount = existing.LikeCount,
                            DislikeCount = existing.DislikeCount,
                            MyReaction = own.MyReaction
                        };
                    }
                    else
                    {
                        summaries[own.WorldUserCommentId] = new CommentReactionSummary
                        {
                            MyReaction = own.MyReaction
                        };
                    }
                }
            }

            return summaries;
        }

        private async Task<CommentReactionSummary> GetCommentReactionSummaryAsync(int commentId, string currentUserHash, CancellationToken cancellationToken = default)
        {
            var stats = await GetCommentReactionStatsAsync(new List<int> { commentId }, currentUserHash, cancellationToken);
            return stats.TryGetValue(commentId, out var summary)
                ? summary
                : new CommentReactionSummary();
        }

        private async Task<HashSet<int>> GetReportedCommentIdsAsync(List<int> commentIds, string currentUserHash, CancellationToken cancellationToken = default)
        {
            if (commentIds == null || commentIds.Count == 0 || string.IsNullOrWhiteSpace(currentUserHash))
                return new HashSet<int>();

            var ids = await _context.WorldUserCommentReports
                .AsNoTracking()
                .Where(x => commentIds.Contains(x.WorldUserCommentId) && x.UserHash == currentUserHash)
                .Select(x => x.WorldUserCommentId)
                .ToListAsync(cancellationToken);

            return ids.ToHashSet();
        }

        private async Task<Dictionary<int, int>> GetCommentReportCountsAsync(List<int> commentIds, CancellationToken cancellationToken = default)
        {
            if (commentIds == null || commentIds.Count == 0)
                return new Dictionary<int, int>();

            return await _context.WorldUserCommentReports
                .AsNoTracking()
                .Where(x => commentIds.Contains(x.WorldUserCommentId))
                .GroupBy(x => x.WorldUserCommentId)
                .Select(g => new { CommentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CommentId, x => x.Count, cancellationToken);
        }

        private async Task TryAddCommentNotificationsAsync(WorldUserPosting posting, WorldUserComment newComment, WorldUserComment? parentComment, CancellationToken cancellationToken)
        {
            try
            {
                if (posting == null || newComment == null)
                    return;

                await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

                var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var excerpt = BuildCommentNotificationExcerpt(newComment.CommentText);
                var href = BuildCommentNotificationHref(posting, newComment);

                if (!string.IsNullOrWhiteSpace(parentComment?.UserHash)
                    && !string.Equals(parentComment.UserHash, newComment.UserHash, StringComparison.OrdinalIgnoreCase))
                {
                    recipients.Add(parentComment.UserHash);
                }

                if (!string.IsNullOrWhiteSpace(posting.CreatorId)
                    && !string.Equals(posting.CreatorId, newComment.UserHash, StringComparison.OrdinalIgnoreCase))
                {
                    recipients.Add(posting.CreatorId);
                }

                foreach (var recipientUserHash in recipients)
                {
                    var isReplyTarget = !string.IsNullOrWhiteSpace(parentComment?.UserHash)
                        && string.Equals(parentComment.UserHash, recipientUserHash, StringComparison.OrdinalIgnoreCase);
                    var isCreatorTarget = string.Equals(posting.CreatorId, recipientUserHash, StringComparison.OrdinalIgnoreCase);

                    string description;
                    string icon;
                    string sender;

                    if (isReplyTarget)
                    {
                        icon = "bi-reply-fill";
                        sender = "comment-reply";
                        description = string.IsNullOrWhiteSpace(excerpt)
                            ? $"{newComment.UserName} hat auf deinen Kommentar geantwortet."
                            : $"{newComment.UserName} hat auf deinen Kommentar geantwortet.\n\"{excerpt}\"";
                    }
                    else if (isCreatorTarget)
                    {
                        icon = "bi-chat-dots-fill";
                        sender = "comment-video";
                        var targetTitle = string.IsNullOrWhiteSpace(posting.Title) ? "dein Video" : posting.Title.Trim();
                        description = string.IsNullOrWhiteSpace(excerpt)
                            ? $"{newComment.UserName} hat {targetTitle} kommentiert."
                            : $"{newComment.UserName} hat {targetTitle} kommentiert.\n\"{excerpt}\"";
                    }
                    else
                    {
                        continue;
                    }

                    var exists = await _context.WorldUserNotifications
                        .AsNoTracking()
                        .AnyAsync(x => x.UserHash == recipientUserHash
                            && x.Sender == sender
                            && x.Href == href
                            && x.Description == description,
                            cancellationToken);

                    if (exists)
                        continue;

                    _context.WorldUserNotifications.Add(new WorldUserNotification
                    {
                        UserHash = recipientUserHash,
                        Icon = icon,
                        Sender = sender,
                        Description = description,
                        Href = href,
                        CreatedAtUtc = DateTime.UtcNow,
                        IsSeen = false,
                        SeenAtUtc = null
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        private static string BuildCommentNotificationHref(WorldUserPosting posting, WorldUserComment comment)
        {
            var recipeId = posting?.Recipe?.Id ?? 0;
            var scrollToId = recipeId > 0 ? recipeId : posting?.Id ?? 0;
            var postingId = posting?.Id ?? 0;
            var commentId = comment?.Id ?? 0;
            return $"/WorldMiniApp/Feed?scrollToId={scrollToId}&openComments=1&postingId={postingId}&commentId={commentId}";
        }

        private static string BuildCommentNotificationExcerpt(string? text)
        {
            var normalized = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            normalized = normalized.Replace("\r", " ").Replace("\n", " ");
            return normalized.Length <= 120
                ? normalized
                : $"{normalized[..117].TrimEnd()}...";
        }

        private async Task TryAddLikeNotificationAsync(string likerUserHash, string likerName, int recipeId, CancellationToken cancellationToken)
        {
            try
            {
                var posting = await _context.WorldUserPosting
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Recipe != null && x.Recipe.Id == recipeId, cancellationToken);

                if (posting == null || string.IsNullOrWhiteSpace(posting.CreatorId))
                    return;

                // Don't notify yourself
                if (string.Equals(posting.CreatorId, likerUserHash, StringComparison.OrdinalIgnoreCase))
                    return;

                await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

                var recipeTitle = string.IsNullOrWhiteSpace(posting.Title) ? "dein Video" : posting.Title.Trim();
                var description = $"{likerName} hat {recipeTitle} geliked.";
                var href = $"/WorldMiniApp/Feed?scrollToId={recipeId}";
                var sender = "like-video";
                var icon = "bi-heart-fill";

                var exists = await _context.WorldUserNotifications
                    .AsNoTracking()
                    .AnyAsync(x => x.UserHash == posting.CreatorId
                        && x.Sender == sender
                        && x.Href == href
                        && x.Description == description,
                        cancellationToken);

                if (exists)
                    return;

                _context.WorldUserNotifications.Add(new WorldUserNotification
                {
                    UserHash = posting.CreatorId,
                    Icon = icon,
                    Sender = sender,
                    Description = description,
                    Href = href,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsSeen = false,
                    SeenAtUtc = null
                });

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        private async Task TryAddCommentLikeNotificationAsync(string likerUserHash, string likerName, int commentId, CancellationToken cancellationToken)
        {
            try
            {
                var comment = await _context.WorldUserComments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);

                if (comment == null || string.IsNullOrWhiteSpace(comment.UserHash))
                    return;

                // Don't notify yourself
                if (string.Equals(comment.UserHash, likerUserHash, StringComparison.OrdinalIgnoreCase))
                    return;

                await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

                var excerpt = BuildCommentNotificationExcerpt(comment.CommentText);
                var description = string.IsNullOrWhiteSpace(excerpt)
                    ? $"{likerName} hat deinen Kommentar geliked."
                    : $"{likerName} hat deinen Kommentar geliked.\n\"{excerpt}\"";

                var posting = await _context.WorldUserPosting
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == comment.WorldUserPostingId, cancellationToken);

                var recipeId = posting?.Recipe?.Id ?? 0;
                var scrollToId = recipeId > 0 ? recipeId : posting?.Id ?? 0;
                var href = $"/WorldMiniApp/Feed?scrollToId={scrollToId}&openComments=1&postingId={comment.WorldUserPostingId}&commentId={commentId}";
                var sender = "like-comment";
                var icon = "bi-heart-fill";

                var exists = await _context.WorldUserNotifications
                    .AsNoTracking()
                    .AnyAsync(x => x.UserHash == comment.UserHash
                        && x.Sender == sender
                        && x.Href == href
                        && x.Description == description,
                        cancellationToken);

                if (exists)
                    return;

                _context.WorldUserNotifications.Add(new WorldUserNotification
                {
                    UserHash = comment.UserHash,
                    Icon = icon,
                    Sender = sender,
                    Description = description,
                    Href = href,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsSeen = false,
                    SeenAtUtc = null
                });

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        private async Task EnsureWorldUserCommentsSchemaAsync(CancellationToken cancellationToken = default)
        {
            if (CommentsSchemaEnsured) return;

            await EnsureCommentsSchemaLock.WaitAsync(cancellationToken);
            try
            {
                if (CommentsSchemaEnsured) return;
                await _context.Database.ExecuteSqlRawAsync(EnsureWorldUserCommentsSchemaSql, cancellationToken);
                CommentsSchemaEnsured = true;
            }
            finally
            {
                EnsureCommentsSchemaLock.Release();
            }
        }

        private async Task EnsureWorldUserNotificationsSchemaAsync(CancellationToken cancellationToken = default)
        {
            if (NotificationsSchemaEnsured) return;

            await EnsureNotificationsSchemaLock.WaitAsync(cancellationToken);
            try
            {
                if (NotificationsSchemaEnsured) return;
                await _context.Database.ExecuteSqlRawAsync(EnsureWorldUserNotificationsSchemaSql, cancellationToken);
                NotificationsSchemaEnsured = true;
            }
            finally
            {
                EnsureNotificationsSchemaLock.Release();
            }
        }



        private async Task<NutritionTotals> BuildNutritionTotalsAsync(List<int> recipeIds)
        {
            var recipes = await _context.RecipeBaseData
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                .Where(r => recipeIds.Contains(r.Id))
                .AsNoTracking()
                .ToListAsync();

            var totals = new NutritionTotals();
            foreach (var recipe in recipes)
            {
                if (recipe.Ingredients == null) continue;
                foreach (var entry in recipe.Ingredients)
                {
                    var ingredient = entry.Ingredient;
                    var nutrient = ingredient?.IngredientsAndNutrients;
                    if (nutrient == null) continue;
                    var quantity = ingredient.Quantity?.Quantitys ?? 0;
                    var unit = ingredient.Measure?.Metrics_DE ?? string.Empty;
                    var grams = (decimal)quantity;
                    if (string.Equals(unit, "Stk.", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(unit, "Stück", StringComparison.OrdinalIgnoreCase))
                    {
                        var weightPerPiece = nutrient.Weight_per_piece > 0 ? nutrient.Weight_per_piece : 0;
                        grams = (decimal)weightPerPiece * (decimal)quantity;
                    }
                    if (grams <= 0) continue;
                    totals.Calories += ((decimal)nutrient.Calories_a_100g * grams) / 100m;
                    totals.Fat += (nutrient.Fat_a_100g * grams) / 100m;
                    totals.Carbohydrates += (nutrient.Carbohydrates_a_100g * grams) / 100m;
                    totals.Protein += (nutrient.Protein_a_100g * grams) / 100m;
                }
            }

            totals.Calories = Math.Round(totals.Calories, 0);
            totals.Fat = Math.Round(totals.Fat, 1);
            totals.Carbohydrates = Math.Round(totals.Carbohydrates, 1);
            totals.Protein = Math.Round(totals.Protein, 1);
            return totals;
        }

        private static List<string> ExtractTags(string description, NutritionTotals? nutrition)
        {
            var tags = new List<string>();
            var desc = description.ToLowerInvariant();
            if (desc.Contains("high protein") || desc.Contains("high-protein") || desc.Contains("proteinreich"))
                tags.Add("High-Protein");
            if (desc.Contains("low carb") || desc.Contains("low-carb"))
                tags.Add("Low Carb");
            if (desc.Contains("vegan"))
                tags.Add("Vegan");
            else if (desc.Contains("vegetarisch") || desc.Contains("vegetarian"))
                tags.Add("Vegetarisch");
            if (desc.Contains("keto"))
                tags.Add("Keto");
            if (desc.Contains("diät") || desc.Contains("diet") || desc.Contains("abnehm"))
                tags.Add("Diät");
            if (tags.Count == 0 && nutrition != null)
            {
                if (nutrition.Protein > 0 && nutrition.Calories > 0 && (nutrition.Protein * 4 / nutrition.Calories) > 0.30m)
                    tags.Add("High-Protein");
                if (nutrition.Carbohydrates > 0 && nutrition.Calories > 0 && (nutrition.Carbohydrates * 4 / nutrition.Calories) < 0.20m)
                    tags.Add("Low Carb");
            }
            return tags;
        }

        private class NutritionTotals
        {
            public decimal Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Fat { get; set; }
            public decimal Carbohydrates { get; set; }
        }
    }
}

