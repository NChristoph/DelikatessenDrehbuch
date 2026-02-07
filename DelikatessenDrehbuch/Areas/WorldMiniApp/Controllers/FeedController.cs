using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class FeedController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
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

        public FeedController(ApplicationDbContext context, ILogger<FeedController> logger)
        {
         _context = context;   
            _logger = logger;
        }
        //TODO:Likecount zu basedata recipe hinzufügen und abo system auch machen neue column auserdem brauchen 
        //wir noch eine ide damit die likes rot sind wen wir sie geliket haben
        //TodoThumbAutomatisch speichern
        public async Task<IActionResult> Index(string filter = "feed", string userHash = "", int scrollToId = 0, string searchTerm = "", string category = "", int? maxPrepTime = null)
        {
            userHash = ResolveUserHash(userHash);
            List<WorldUserPosting> model = new List<WorldUserPosting>();

            
            var baseQuery = _context.WorldUserPosting
                .AsNoTracking()
                .Include(p => p.Recipe)
                .ThenInclude(r => r.RecipeKeywords)
                .ThenInclude(link => link.Keyword);

            IQueryable<WorldUserPosting> query = baseQuery;

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
                query = query.Where(post => post.Recipe.PreperationTime <= maxPrepTime.Value);
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

           
            ViewData["ScrollToId"] = scrollToId;
            ViewData["CurrentFilter"] = filter;
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Category"] = category;
            ViewData["MaxPrepTime"] = maxPrepTime?.ToString() ?? string.Empty;
            ViewData["UserHash"] = userHash;

            return View(model);
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

            // --- BASIS DATEN (Für alle) ---

            // 1. Likes laden
            var likes = _context.WorldUserLike.Where(x => x.WorldAppUser.UserHash == userHash).Select(x => x.Recipe.Id);

            var likedRecipes = await _context.WorldUserPosting.Where(x => likes.Contains(x.Recipe.Id)).Include(x => x.Recipe).ToListAsync();
            

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

            // Prüfung auf "orb"
            if (user.IsVerified == "orb" || user.UserHash == "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839")
            {
                // 3. Eigene Videos laden
                myVideos = await _context.WorldUserPosting
                    .Where(p => p.CreatorId == user.UserHash) // Oder User.Id, je nach deiner DB
                    .Include(p => p.Recipe).ThenInclude(r => r.Images)
                    .OrderByDescending(p => p.CreationTime)
                    .ToListAsync();

                // 4. Follower zählen (Wer folgt mir?)
                followerCount = await _context.WorldUserAbo
                    .CountAsync(a => a.Creator.Id == user.Id);
            }

            var model = new UserProfileViewModel
            {
                User = user,
                LikedRecipes = likedRecipes,
                Following = following,
                MealPlans = mealPlans,
                MyVideos = myVideos,
                FollowerCount = followerCount
            };

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

        private string ResolveUserHash(string userHash)
        {
            var sessionHash = HttpContext.Session.GetString(SessionUserHashKey);
            if (string.IsNullOrWhiteSpace(sessionHash))
            {
                return string.Empty;
            }

            return sessionHash;
        }


    }
}
