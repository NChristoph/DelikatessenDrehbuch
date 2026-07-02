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
using WorldMiniApp.Services;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class FeedController : WorldMiniAppBaseController
    {
        private const string SessionWalletWLD = "WorldWallet_WLD";
        private const string SessionWalletUSDT = "WorldWallet_USDT";
        private static readonly Dictionary<string, string[]> CategoryAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["appetizer"] = new[] { "appetizer", "aperetizer", "vorspeise", "entrada" },
            ["main"] = new[] { "main", "maincourse", "hauptspeise", "platoprincipal", "pratoprincipal" },
            ["dessert"] = new[] { "dessert", "postre", "sobremesa", "nachspeise" },
            ["breakfast"] = new[] { "breakfast", "frühstück", "fruehstueck", "desayuno", "cafédamanhã", "cafedamanha" },
            ["lunch"] = new[] { "lunch", "mittag", "almuerzo", "almoço", "almoco" },
            ["dinner"] = new[] { "dinner", "abend", "cena", "jantar" }
        };

        private static readonly Dictionary<string, string> CategoryAliasLookup = CategoryAliases
            .SelectMany(group => group.Value.Select(alias => new KeyValuePair<string, string>(alias, group.Key)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _context;
        private readonly ILogger<FeedController> _logger;
        private readonly IWildCoinService _coinService;
        private readonly IConfiguration _configuration;
        private readonly IFeedAlgorithmService _feedAlgorithmService;

        // SuperUserHash kommt jetzt aus WorldMiniAppBaseController.

        public FeedController(ApplicationDbContext context, ILogger<FeedController> logger, IWildCoinService coinService, IConfiguration configuration, IFeedAlgorithmService feedAlgorithmService)
        {
            _context = context;
            _logger = logger;
            _coinService = coinService;
            _configuration = configuration;
            _feedAlgorithmService = feedAlgorithmService;
        }
        
        public async Task<IActionResult> Index(string filter = "feed", int scrollToId = 0, string searchTerm = "", string category = "", int? maxPrepTime = null)
        {
            var userHash = ResolveUserHash();
            List<WorldUserPosting> model = new List<WorldUserPosting>();

            
            var baseQuery = _context.WorldUserPosting
                .AsNoTracking()
                .Include(p => p.Recipe)
                .ThenInclude(r => r.RecipeKeywords)
                .ThenInclude(link => link.Keyword);

            IQueryable<WorldUserPosting> query = baseQuery
                .Where(p => !p.IsOffline && !p.IsHiddenPendingReview);

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
            ViewData["CanWriteComments"] = await CanWriteCommentsAsync(_context, userHash);

            // User Verification Level für Upload-Button
            // In Debug-Mode oder für SuperUser: Upload erlauben
            var isLocalRequest = string.Equals(HttpContext.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(HttpContext.Request.Host.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
            var isDebugMode = System.Diagnostics.Debugger.IsAttached || isLocalRequest;

            const string devTestHash2 = "0x7c1f6a4be3c2d9aa51e4c0bf2a6e7d8f9b1c3d5e7f8091a2b3c4d5e6f7081920";
            var isTestHash = string.Equals(userHash, SuperUserHash, StringComparison.OrdinalIgnoreCase)
                || string.Equals(userHash, devTestHash2, StringComparison.OrdinalIgnoreCase);

            var userForVerification = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserHash == userHash);

            var isOrbVerified = string.Equals(userForVerification?.IsVerified, "orb", StringComparison.OrdinalIgnoreCase);

            ViewData["UserVerificationLevel"] = userForVerification?.IsVerified ?? "device";
            ViewData["IsOrbVerified"] = isOrbVerified || isDebugMode || isTestHash;

            ViewData["ScrollToId"] = scrollToId;
            ViewData["CurrentFilter"] = filter;
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Category"] = category;
            ViewData["MaxPrepTime"] = maxPrepTime?.ToString() ?? string.Empty;
            ViewData["UserHash"] = userHash;
            // Marktplatz-Daten defensiv laden: falls die DB Probleme macht (z. B. neue Spalten noch
            // nicht per SQL angelegt), soll der Feed trotzdem laden – leerer Marktplatz statt kompletter
            // Fehlerseite ("HTTP ERROR 200" durch abgebrochenes Rendering).
            ViewData["MarketplaceListings"] = new List<MealPlanListing>();
            ViewData["CreatorShopCards"] = new List<PlanCardViewModel>();
            try
            {
            var marketplaceListings = await _context.MealPlanListings
                .Include(x => x.MealPlan)
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            var recipeIds = marketplaceListings
                .SelectMany(l => ExtractRecipeIdsFromMealPlanJson(l.MealPlan?.MealPlan))
                .Concat(marketplaceListings.Where(l => l.RecipeId.HasValue).Select(l => l.RecipeId!.Value))
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
                    ListingType = listing.ListingType,
                    CoverImageUrl = ResolveListingCoverImage(listing, recipeMediaMap),
                    StockQuantity = listing.StockQuantity,
                    RequiresShipping = listing.RequiresShipping,
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
            }
            catch (Exception mpEx)
            {
                _logger.LogError(mpEx, "Marktplatz-Daten für den Feed konnten nicht geladen werden (evtl. fehlende DB-Spalten – SQL ausführen).");
            }

            ViewData["WalletWLD"] = HttpContext.Session.GetString(SessionWalletWLD) ?? "";
            ViewData["WalletUSDT"] = HttpContext.Session.GetString(SessionWalletUSDT) ?? "";
            ViewData["WorldChainId"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:ChainId"] ?? "480";
            ViewData["WorldChainWldToken"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:WldTokenAddress"] ?? "";
            ViewData["WorldChainUsdtToken"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:UsdtTokenAddress"] ?? "";
            ViewData["WorldChainMarketplace"] = HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:MarketplaceContractAddress"] ?? "";
            ViewData["WorldChainTestMode"] = bool.TryParse(HttpContext.RequestServices.GetService<IConfiguration>()?["WorldChain:TestMode"], out var testMode) && testMode;

            return View(model);
        }

        // Titelbild eines Listings: explizites CoverImage hat Vorrang; für Einzelrezepte
        // wird das Bild des Rezepts aus der Media-Map verwendet (analog MarketplaceController).
        private static string? ResolveListingCoverImage(MealPlanListing listing, Dictionary<int, (string ImageUrl, string RecipeTitle)> media)
        {
            if (!string.IsNullOrWhiteSpace(listing.CoverImageUrl)) return listing.CoverImageUrl;
            if (listing.ListingType == MarketplaceListingType.SingleRecipe
                && listing.RecipeId is int rid
                && media.TryGetValue(rid, out var m)
                && !string.IsNullOrWhiteSpace(m.ImageUrl))
                return m.ImageUrl;
            return listing.CoverImageUrl;
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
            return lower.Contains(".mp4")
                || lower.Contains(".mov")
                || lower.Contains(".webm")
                || lower.Contains(".m3u8")
                || lower.Contains("mediadelivery.net/play/");
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
        public async Task<IActionResult> ToggleLike(int recipeId)
        {
            var userHash = ResolveUserHash();
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
        public async Task<IActionResult> ToggleFollow([FromForm] string creatorId)
        {
            var userHash = ResolveUserHash();

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


        public async Task<IActionResult> MyProfile()
        {
            var userHash = ResolveUserHash();
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
            
           
            var rawAiVariants = await _context.RecipeUserVariants
                .AsNoTracking()
                .Where(x => x.UserHash == userHash)
                .Select(x => new { x.Id, BaseRecipeId = x.OriginalRecipeId, x.Title, CreatedAtUtc = x.CreatedAtUtc })
                .ToListAsync();

            var aiRecipeIds = rawAiVariants.Select(x => x.BaseRecipeId).Distinct().ToList();
            var aiMediaMap = await BuildRecipeMediaMapAsync(aiRecipeIds);

            var aiEditedCards = rawAiVariants
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(80)
                .Select(v =>
                {
                    var media = aiMediaMap.TryGetValue(v.BaseRecipeId, out var m) ? m : (ImageUrl: string.Empty, RecipeTitle: string.Empty);
                    var baseTitle = string.IsNullOrWhiteSpace(media.RecipeTitle) ? "Rezept" : media.RecipeTitle;
                    const string variantType = "swap";
                    const string variantLabel = "Variante";

                    return new WorldAiEditedRecipeCard
                    {
                        AiVariantId = v.Id,
                        BaseRecipeId = v.BaseRecipeId,
                        BaseTitle = baseTitle,
                        VariantTitle = string.IsNullOrWhiteSpace(v.Title) ? baseTitle : v.Title,
                        ImageUrl = media.ImageUrl,
                        VariantType = variantType,
                        VariantLabel = variantLabel,
                        UpdatedAtUtc = v.CreatedAtUtc
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

            // Pr�fung auf "orb" oder SuperUser
            if (user.IsVerified == "orb" || user.UserHash == SuperUserHash)
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

            // Load hero images for purchases
            foreach (var purchase in purchases)
            {
                if (purchase.Listing?.MealPlan?.MealPlan != null)
                {
                    try
                    {
                        // Parse JSON using Newtonsoft.Json for more flexibility
                        var jObject = Newtonsoft.Json.Linq.JObject.Parse(purchase.Listing.MealPlan.MealPlan);
                        var firstRecipeId = 0;

                        // Search through all days for the first recipe
                        foreach (var day in jObject.Properties())
                        {
                            if (day.Value is Newtonsoft.Json.Linq.JArray meals && meals.Count > 0)
                            {
                                foreach (var meal in meals)
                                {
                                    // Check if meal is a direct integer (just the recipe ID)
                                    if (meal.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                                    {
                                        firstRecipeId = (int)meal;
                                        if (firstRecipeId > 0) break;
                                    }
                                    // Check if meal is an object with recipeId property
                                    else if (meal.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                                    {
                                        var recipeIdToken = meal["recipeId"];
                                        if (recipeIdToken != null && recipeIdToken.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                                        {
                                            firstRecipeId = (int)recipeIdToken;
                                            if (firstRecipeId > 0) break;
                                        }
                                    }
                                }
                                if (firstRecipeId > 0) break;
                            }
                        }

                        if (firstRecipeId > 0)
                        {
                            var recipe = await _context.RecipeBaseData
                                .Include(r => r.Images)
                                .FirstOrDefaultAsync(r => r.Id == firstRecipeId);

                            if (recipe?.Images != null && recipe.Images.Count > 0)
                            {
                                ViewData[$"PurchaseHeroImage_{purchase.Id}"] = NormalizeRecipeImagePath(recipe.Images.First().Image);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error for debugging
                        System.Diagnostics.Debug.WriteLine($"Error loading hero image for purchase {purchase.Id}: {ex.Message}");
                    }
                }
            }

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

            ViewData["IsWorldMiniAppAdmin"] = IsCommentModeratorAuthenticated(userHash);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string userName, string? storeUrl = null)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (user == null)
            {
                return NotFound();
            }

            var changed = false;

            if (!string.IsNullOrWhiteSpace(userName))
            {
                user.UserName = userName.Trim();
                changed = true;
            }

            // Store-URL: leer = löschen; sonst nur gültige http/https-URLs (max. 1024 Zeichen).
            if (storeUrl != null)
            {
                var trimmed = storeUrl.Trim();
                if (trimmed.Length == 0)
                {
                    user.StoreUrl = null;
                    changed = true;
                }
                else if (trimmed.Length <= 1024
                         && Uri.TryCreate(trimmed, UriKind.Absolute, out var u)
                         && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                {
                    user.StoreUrl = trimmed;
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(MyProfile), new { userHash });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPostingOffline([FromForm] int postingId)
        {
            var userHash = ResolveUserHash();
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
        public async Task<IActionResult> SetPostingOnline([FromForm] int postingId)
        {
            var userHash = ResolveUserHash();
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

        // ResolveUserHash(string) kommt jetzt aus WorldMiniAppBaseController.

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

                    var href = $"/WorldMiniApp/Feed?scrollToId={recipeId}";
                var contextText = string.IsNullOrWhiteSpace(posting.Title) ? string.Empty : posting.Title.Trim();
                await UpsertInteractionNotificationAsync(
                    _context,
                    posting.CreatorId,
                    "like-video",
                    "bi-heart-fill",
                    href,
                    $"like-video:{posting.Id}",
                    likerName,
                    contextText,
                    cancellationToken);
            }
            catch
            {
                // best-effort
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

        /// <summary>
        /// Trackt User-Interaktionen für den Feed-Algorithmus (kein AntiForgeryToken nötig für AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TrackInteraction([FromBody] TrackInteractionRequest request)
        {
            var userHash = ResolveUserHash(request.UserHash);
            if (string.IsNullOrEmpty(userHash))
            {
                return Ok(); // Nicht eingeloggt, kein Tracking
            }

            try
            {
                await _feedAlgorithmService.TrackInteractionAsync(
                    userHash: userHash,
                    postingId: request.PostingId,
                    type: request.InteractionType,
                    duration: request.Duration
                );

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Tracking von Interaction: {Type} für Posting {PostingId}",
                    request.InteractionType, request.PostingId);
                return Ok(); // Tracking-Fehler sollten nicht die UX beeinträchtigen
            }
        }

        // POST: Feed/ReportPosting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportPosting([FromBody] ReportPostingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserHash) || request.PostingId <= 0)
            {
                return BadRequest(new { success = false, message = "Ungültige Anfrage." });
            }

            try
            {
                // 1. Cooldown Check: Max 5 Reports pro Stunde
                var oneHourAgo = DateTime.UtcNow.AddHours(-1);
                var recentReportCount = await _context.WorldUserReports
                    .Where(r => r.ReporterHash == request.UserHash && r.CreatedAt >= oneHourAgo)
                    .CountAsync();

                if (recentReportCount >= 5)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Du hast zu viele Inhalte in kurzer Zeit gemeldet. Bitte warte eine Weile.",
                        cooldown = true
                    });
                }

                // 2. Prüfe ob User diesen Post bereits gemeldet hat
                var existingReport = await _context.WorldUserReports
                    .FirstOrDefaultAsync(r => r.PostingId == request.PostingId && r.ReporterHash == request.UserHash);

                if (existingReport != null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Du hast diesen Inhalt bereits gemeldet.",
                        alreadyReported = true
                    });
                }

                // 3. Prüfe ob Posting existiert
                var posting = await _context.WorldUserPosting.FirstOrDefaultAsync(p => p.Id == request.PostingId);
                if (posting == null)
                {
                    return NotFound(new { success = false, message = "Inhalt nicht gefunden." });
                }

                // 4. Erstelle Report
                var report = new WorldUserReport
                {
                    ReporterHash = request.UserHash,
                    PostingId = request.PostingId,
                    Reason = request.Reason ?? "other",
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow,
                    Status = "pending"
                };

                await _context.WorldUserReports.AddAsync(report);

                // 5. Erhöhe ReportCount
                posting.ReportCount++;

                // 6. Ab 3 Reports → Automatisch verstecken
                if (posting.ReportCount >= 3 && !posting.IsHiddenPendingReview)
                {
                    posting.IsHiddenPendingReview = true;
                    _logger.LogWarning(
                        "Content auto-hidden due to reports. PostingId={PostingId}, ReportCount={ReportCount}",
                        posting.Id, posting.ReportCount);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "User {UserHash} reported posting {PostingId} for {Reason}. Total reports: {Count}",
                    request.UserHash, request.PostingId, request.Reason, posting.ReportCount);

                return Ok(new
                {
                    success = true,
                    message = "Danke für deine Meldung. Wir werden den Inhalt prüfen.",
                    reportCount = posting.ReportCount,
                    isHidden = posting.IsHiddenPendingReview
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Melden von Posting {PostingId}", request.PostingId);
                return StatusCode(500, new { success = false, message = "Ein Fehler ist aufgetreten." });
            }
        }

        public class ReportPostingRequest
        {
            public string UserHash { get; set; } = string.Empty;
            public int PostingId { get; set; }
            public string? Reason { get; set; } // "spam", "inappropriate", "copyright", "other"
            public string? Description { get; set; }
        }

        public class TrackInteractionRequest
        {
            public string UserHash { get; set; } = string.Empty;
            public int PostingId { get; set; }
            public InteractionType InteractionType { get; set; }
            public double? Duration { get; set; }
        }
    }
}
