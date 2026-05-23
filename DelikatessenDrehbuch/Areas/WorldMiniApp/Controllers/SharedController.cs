using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    public class SharedController : WorldMiniAppBaseController
    {
        private const string WorldMiniAppId = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SharedController> _logger;

        public SharedController(ApplicationDbContext context, ILogger<SharedController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult DebugLogs()
        {
            ViewBag.Logs = DebugLogger.GetLogs();
            return View("~/Areas/WorldMiniApp/Views/Shared/Debug.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> FeedLight(string userHash = "", int? feedId = null)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp", returnTo = "/WorldMiniApp/Shared/FeedLight" });
            }

            var feedData = await _context.WorldSharedFeedMembers
                .AsNoTracking()
                .Where(x => x.UserHash == userHash)
                .Join(_context.WorldSharedFeeds.AsNoTracking(),
                    member => member.WorldSharedFeedId,
                    feed => feed.Id,
                    (member, feed) => new
                    {
                        feed.Id,
                        feed.Title,
                        feed.OwnerUserHash,
                        feed.LastActivityAtUtc
                    })
                .ToListAsync();

            var memberships = feedData.Select(feed => new SharedFeedListItemViewModel
            {
                Id = feed.Id,
                Title = feed.Title,
                MemberCount = _context.WorldSharedFeedMembers.Count(m => m.WorldSharedFeedId == feed.Id),
                ItemCount = _context.WorldSharedFeedItems.Count(i => i.WorldSharedFeedId == feed.Id),
                LastActivityAtUtc = feed.LastActivityAtUtc,
                IsOwner = string.Equals(feed.OwnerUserHash, userHash, StringComparison.OrdinalIgnoreCase)
            })
            .OrderByDescending(x => x.IsOwner)
            .ThenByDescending(x => x.LastActivityAtUtc)
            .ToList();

            SharedFeedDetailViewModel? activeFeed = null;
            var resolvedFeedId = feedId ?? memberships.FirstOrDefault()?.Id;
            if (resolvedFeedId.HasValue)
            {
                activeFeed = await BuildFeedDetailAsync(resolvedFeedId.Value, userHash);
            }

            var model = new SharedFeedIndexViewModel
            {
                CurrentUserHash = userHash,
                Feeds = memberships,
                ActiveFeed = activeFeed
            };

            return View("~/Areas/WorldMiniApp/Views/Shared/FeedLight.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFeed(string title, string userHash = "")
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Geteilte Inhalte" : title.Trim();
            var feed = new WorldSharedFeed
            {
                Title = cleanTitle,
                OwnerUserHash = userHash,
                InviteToken = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTime.UtcNow,
                LastActivityAtUtc = DateTime.UtcNow
            };

            await _context.WorldSharedFeeds.AddAsync(feed);
            await _context.SaveChangesAsync();

            await _context.WorldSharedFeedMembers.AddAsync(new WorldSharedFeedMember
            {
                WorldSharedFeedId = feed.Id,
                UserHash = userHash,
                Role = "owner",
                AddedByUserHash = userHash,
                JoinedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(FeedLight), new { userHash, feedId = feed.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Invite(string token, string userHash = "")
        {
            DebugLogger.Log($"=== INVITE CALLED === Token: {token ?? "NULL"}");
            _logger.LogWarning("=== INVITE ACTION CALLED === Token: {Token}", token ?? "NULL");

            if (string.IsNullOrWhiteSpace(token))
            {
                DebugLogger.Log("Invite: token is null - NotFound");
                _logger.LogWarning("Invite: token is null or empty - returning NotFound");
                return NotFound();
            }

            userHash = ResolveUserHash(userHash);
            DebugLogger.Log($"Invite: userHash: {userHash ?? "NULL"}");
            _logger.LogWarning("Invite: resolved userHash: {UserHash}", userHash ?? "NULL");

            if (string.IsNullOrWhiteSpace(userHash))
            {
                var returnTo = $"/WorldMiniApp/Shared/Invite?token={Uri.EscapeDataString(token)}";
                DebugLogger.Log($"Invite: redirecting to login, returnTo: {returnTo}");
                _logger.LogWarning("Invite: no userHash - redirecting to login with returnTo: {ReturnTo}", returnTo);
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp", returnTo });
            }

            var feed = await _context.WorldSharedFeeds.FirstOrDefaultAsync(x => x.InviteToken == token);
            if (feed == null)
            {
                DebugLogger.Log($"Invite: feed not found for token: {token}");
                _logger.LogWarning("Invite: feed not found for token: {Token}", token);
                return NotFound();
            }

            DebugLogger.Log($"Invite: found feed {feed.Id} '{feed.Title}'");
            _logger.LogWarning("Invite: found feed ID {FeedId}, Title: {Title}", feed.Id, feed.Title);

            var isMember = await _context.WorldSharedFeedMembers
                .AnyAsync(x => x.WorldSharedFeedId == feed.Id && x.UserHash == userHash);

            DebugLogger.Log($"Invite: is member: {isMember}");
            _logger.LogWarning("Invite: user is already member: {IsMember}", isMember);

            if (!isMember)
            {
                DebugLogger.Log($"Invite: adding user to feed {feed.Id}");
                _logger.LogWarning("Invite: adding user to feed {FeedId}", feed.Id);
                await _context.WorldSharedFeedMembers.AddAsync(new WorldSharedFeedMember
                {
                    WorldSharedFeedId = feed.Id,
                    UserHash = userHash,
                    Role = "member",
                    AddedByUserHash = feed.OwnerUserHash,
                    JoinedAtUtc = DateTime.UtcNow
                });

                feed.LastActivityAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                DebugLogger.Log("Invite: user added successfully");
                _logger.LogWarning("Invite: successfully added user to feed");
            }

            DebugLogger.Log($"Invite: redirecting to FeedLight feedId={feed.Id}");
            _logger.LogWarning("Invite: redirecting to FeedLight with feedId: {FeedId}", feed.Id);
            return RedirectToAction(nameof(FeedLight), new { userHash, feedId = feed.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExistingItem(int feedId, string contentType, string sourceToken, string userHash = "")
        {
            userHash = ResolveUserHash(userHash);
            if (!await HasFeedAccessAsync(feedId, userHash))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(sourceToken))
            {
                return RedirectToAction(nameof(FeedLight), new { userHash, feedId });
            }

            var normalizedType = contentType.Trim().ToLowerInvariant();
            var added = await TryAddSharedFeedItemAsync(feedId, normalizedType, sourceToken.Trim(), userHash);
            if (added)
            {
                await TouchFeedAsync(feedId);
            }

            return RedirectToAction(nameof(FeedLight), new { userHash, feedId });
        }

        internal async Task<bool> TryAddSharedFeedItemAsync(int feedId, string contentType, string sourceToken, string addedByUserHash)
        {
            if (!await HasFeedAccessAsync(feedId, addedByUserHash))
            {
                return false;
            }

            var normalizedType = contentType.Trim().ToLowerInvariant();
            if (normalizedType != "mealplan" && normalizedType != "shoppinglist")
            {
                return false;
            }

            var exists = await _context.WorldSharedFeedItems.AnyAsync(x =>
                x.WorldSharedFeedId == feedId &&
                x.ContentType == normalizedType &&
                x.SourceToken == sourceToken);

            if (exists)
            {
                return false;
            }

            string title;
            if (normalizedType == "mealplan")
            {
                var plan = await _context.WorldSharedMealPlan.FirstOrDefaultAsync(x => x.ShareToken == sourceToken);
                if (plan == null)
                {
                    return false;
                }

                title = string.IsNullOrWhiteSpace(plan.Title) ? "Geteilter Essensplan" : plan.Title;
            }
            else
            {
                var list = await _context.WorldSharedShoppingList.FirstOrDefaultAsync(x => x.ShareToken == sourceToken);
                if (list == null)
                {
                    return false;
                }

                title = "Geteilte Einkaufsliste";
            }

            await _context.WorldSharedFeedItems.AddAsync(new WorldSharedFeedItem
            {
                WorldSharedFeedId = feedId,
                ContentType = normalizedType,
                SourceToken = sourceToken,
                Title = title,
                AddedByUserHash = addedByUserHash,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }

        internal async Task TouchFeedAsync(int feedId)
        {
            var feed = await _context.WorldSharedFeeds.FirstOrDefaultAsync(x => x.Id == feedId);
            if (feed == null)
            {
                return;
            }

            feed.LastActivityAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task<bool> HasFeedAccessAsync(int feedId, string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            return await _context.WorldSharedFeedMembers
                .AnyAsync(x => x.WorldSharedFeedId == feedId && x.UserHash == userHash);
        }

        private async Task<SharedFeedDetailViewModel?> BuildFeedDetailAsync(int feedId, string userHash)
        {
            var feed = await _context.WorldSharedFeeds
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == feedId);

            if (feed == null)
            {
                return null;
            }

            var isMember = await HasFeedAccessAsync(feedId, userHash);
            if (!isMember)
            {
                return null;
            }

            var users = await _context.WorldAppUser
                .AsNoTracking()
                .Where(x => x.UserHash != null)
                .ToDictionaryAsync(x => x.UserHash!, x => x.UserName ?? "User");

            var members = await _context.WorldSharedFeedMembers
                .AsNoTracking()
                .Where(x => x.WorldSharedFeedId == feedId)
                .OrderBy(x => x.Role == "owner" ? 0 : 1)
                .ThenBy(x => x.JoinedAtUtc)
                .ToListAsync();

            var items = await _context.WorldSharedFeedItems
                .AsNoTracking()
                .Where(x => x.WorldSharedFeedId == feedId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync();

            var mealTokens = items.Where(x => x.ContentType == "mealplan").Select(x => x.SourceToken).Distinct().ToList();
            var shoppingTokens = items.Where(x => x.ContentType == "shoppinglist").Select(x => x.SourceToken).Distinct().ToList();

            var plans = mealTokens.Count == 0
                ? new Dictionary<string, WorldSharedMealPlan>()
                : await _context.WorldSharedMealPlan
                    .AsNoTracking()
                    .Where(x => mealTokens.Contains(x.ShareToken))
                    .ToDictionaryAsync(x => x.ShareToken);

            var shoppingLists = shoppingTokens.Count == 0
                ? new Dictionary<string, WorldSharedShoppingList>()
                : await _context.WorldSharedShoppingList
                    .AsNoTracking()
                    .Where(x => shoppingTokens.Contains(x.ShareToken))
                    .ToDictionaryAsync(x => x.ShareToken);

            var todos = await _context.WorldSharedFeedTodos
                .AsNoTracking()
                .Where(x => x.WorldSharedFeedId == feedId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAtUtc)
                .ToListAsync();

            var detail = new SharedFeedDetailViewModel
            {
                Id = feed.Id,
                Title = feed.Title,
                InviteToken = feed.InviteToken,
                InviteUrl = BuildWorldMiniAppShareUrl($"/WorldMiniApp/Shared/Invite?token={Uri.EscapeDataString(feed.InviteToken)}"),
                IsOwner = string.Equals(feed.OwnerUserHash, userHash, StringComparison.OrdinalIgnoreCase),
                CurrentUserHash = userHash,
                Members = members.Select(x => new SharedFeedMemberViewModel
                {
                    UserHash = x.UserHash,
                    DisplayName = users.TryGetValue(x.UserHash, out var name) ? name : ShortHash(x.UserHash),
                    Role = x.Role
                }).ToList(),
                Items = new List<SharedFeedContentCardViewModel>(),
                Todos = todos.Select(t => new SharedFeedTodoViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    IsCompleted = t.IsCompleted,
                    CreatedByName = users.TryGetValue(t.CreatedByUserHash, out var creator) ? creator : ShortHash(t.CreatedByUserHash),
                    CompletedByName = !string.IsNullOrEmpty(t.CompletedByUserHash) && users.TryGetValue(t.CompletedByUserHash, out var completer) ? completer : null,
                    CreatedAtUtc = t.CreatedAtUtc,
                    CompletedAtUtc = t.CompletedAtUtc
                }).ToList()
            };

            foreach (var item in items)
            {
                if (item.ContentType == "mealplan" && plans.TryGetValue(item.SourceToken, out var plan))
                {
                    var planItems = JsonConvert.DeserializeObject<List<MealPlanHelperMobile>>(plan.MealPlanJson) ?? new List<MealPlanHelperMobile>();
                    var recipeIds = planItems.Select(x => x.RecipeId).Distinct().ToList();

                    var recipes = await _context.RecipeBaseData
                        .AsNoTracking()
                        .Where(x => recipeIds.Contains(x.Id))
                        .Select(x => new
                        {
                            x.Id,
                            x.Title,
                            x.PreparationTime,
                            x.Category,
                            Image = _context.RecipeBaseDataImage
                                .Where(img => img.Recipe.Id == x.Id && img.WorldAppImage)
                                .Select(img => img.Image)
                                .FirstOrDefault()
                        })
                        .ToListAsync();

                    var dishes = recipes.Take(6).Select(r => new RecipeDishPreview
                    {
                        Name = r.Title,
                        ImageUrl = r.Image ?? "/images/placeholder-recipe.jpg",
                        PreparationTime = r.PreparationTime,
                        Category = r.Category ?? "Hauptgericht"
                    }).ToList();

                    var maxDayIndex = planItems.Any() ? planItems.Max(x => x.DayIndex) + 1 : 0;

                    detail.Items.Add(new SharedFeedContentCardViewModel
                    {
                        ContentType = "mealplan",
                        Title = string.IsNullOrWhiteSpace(plan.Title) ? item.Title : plan.Title,
                        SourceToken = item.SourceToken,
                        OpenUrl = $"/WorldMiniApp/MealPlan/SharedMealPlan?token={Uri.EscapeDataString(item.SourceToken)}&direct=true",
                        AddedByName = users.TryGetValue(item.AddedByUserHash, out var addedByPlan) ? addedByPlan : ShortHash(item.AddedByUserHash),
                        CreatorUserHash = plan.UserHash ?? string.Empty,
                        IsCreator = string.Equals(plan.UserHash, userHash, StringComparison.OrdinalIgnoreCase),
                        CreatedAtUtc = item.CreatedAtUtc,
                        PersonCount = plan.PersonCount,
                        MealCount = planItems.Count,
                        RecipeCount = recipeIds.Count,
                        PreviewText = "Meal Plan",
                        Dishes = dishes,
                        DurationDays = maxDayIndex
                    });
                }
                else if (item.ContentType == "shoppinglist" && shoppingLists.TryGetValue(item.SourceToken, out var list))
                {
                    var listItems = JsonConvert.DeserializeObject<List<ShoppingListItem>>(list.ItemsJson) ?? new List<ShoppingListItem>();
                    var preview = string.Join(" • ", listItems.Select(x => x.IngredientName).Where(x => !string.IsNullOrWhiteSpace(x)).Take(4));
                    detail.Items.Add(new SharedFeedContentCardViewModel
                    {
                        ContentType = "shoppinglist",
                        Title = item.Title,
                        SourceToken = item.SourceToken,
                        OpenUrl = $"/WorldMiniApp/MealPlan/SharedShoppingList?token={Uri.EscapeDataString(item.SourceToken)}&direct=true",
                        AddedByName = users.TryGetValue(item.AddedByUserHash, out var addedByList) ? addedByList : ShortHash(item.AddedByUserHash),
                        CreatorUserHash = list.UserHash ?? string.Empty,
                        IsCreator = string.Equals(list.UserHash, userHash, StringComparison.OrdinalIgnoreCase),
                        CreatedAtUtc = item.CreatedAtUtc,
                        PersonCount = 0,
                        MealCount = 0,
                        RecipeCount = listItems.Count,
                        PreviewText = preview
                    });
                }
            }

            return detail;
        }

        private static string ShortHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "User";
            }

            return value.Length <= 10 ? value : $"{value[..6]}...{value[^4..]}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSharedMealPlan(string token, string userHash = "", int? feedId = null)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Token fehlt.");
            }

            var sharedPlan = await _context.WorldSharedMealPlan.FirstOrDefaultAsync(x => x.ShareToken == token);
            if (sharedPlan == null)
            {
                return NotFound("Essensplan nicht gefunden.");
            }

            if (!string.Equals(sharedPlan.UserHash, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var feedItems = await _context.WorldSharedFeedItems
                .Where(x => x.ContentType == "mealplan" && x.SourceToken == token)
                .ToListAsync();

            _context.WorldSharedFeedItems.RemoveRange(feedItems);
            _context.WorldSharedMealPlan.Remove(sharedPlan);
            await _context.SaveChangesAsync();

            if (feedId.HasValue)
            {
                await TouchFeedAsync(feedId.Value);
            }

            return RedirectToAction(nameof(FeedLight), new { userHash, feedId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSharedShoppingList(string token, string userHash = "", int? feedId = null)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Token fehlt.");
            }

            var sharedList = await _context.WorldSharedShoppingList.FirstOrDefaultAsync(x => x.ShareToken == token);
            if (sharedList == null)
            {
                return NotFound("Einkaufsliste nicht gefunden.");
            }

            if (!string.Equals(sharedList.UserHash, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var feedItems = await _context.WorldSharedFeedItems
                .Where(x => x.ContentType == "shoppinglist" && x.SourceToken == token)
                .ToListAsync();

            _context.WorldSharedFeedItems.RemoveRange(feedItems);
            _context.WorldSharedShoppingList.Remove(sharedList);
            await _context.SaveChangesAsync();

            if (feedId.HasValue)
            {
                await TouchFeedAsync(feedId.Value);
            }

            return RedirectToAction(nameof(FeedLight), new { userHash, feedId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessageWithFile([FromForm] int feedId, [FromForm] string userHash, [FromForm] string message, IFormFile file)
        {
            userHash = ResolveUserHash(userHash ?? string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            if (!await HasFeedAccessAsync(feedId, userHash))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("Keine Datei hochgeladen.");
            }

            // Max 10MB
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest("Datei ist zu groß (max. 10 MB).");
            }

            // For now, store file info in message (you can implement actual blob upload later)
            var fileName = Path.GetFileName(file.FileName);
            var fileType = file.ContentType;
            var fileSize = file.Length;

            var messageText = string.IsNullOrWhiteSpace(message)
                ? $"📎 {fileName}"
                : $"{message.Trim()}\n📎 {fileName}";

            var chatMessage = new WorldSharedFeedMessage
            {
                WorldSharedFeedId = feedId,
                UserHash = userHash,
                Message = messageText,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.WorldSharedFeedMessages.AddAsync(chatMessage);
            await _context.SaveChangesAsync();
            await TouchFeedAsync(feedId);

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash);

            return Ok(new
            {
                id = chatMessage.Id,
                userHash = chatMessage.UserHash,
                userName = user?.UserName ?? ShortHash(userHash),
                message = chatMessage.Message,
                createdAtUtc = chatMessage.CreatedAtUtc,
                isOwn = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            var userHash = ResolveUserHash(request.UserHash ?? string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            if (!await HasFeedAccessAsync(request.FeedId, userHash))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 1000)
            {
                return BadRequest("Nachricht ist leer oder zu lang (max. 1000 Zeichen).");
            }

            var message = new WorldSharedFeedMessage
            {
                WorldSharedFeedId = request.FeedId,
                UserHash = userHash,
                Message = request.Message.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.WorldSharedFeedMessages.AddAsync(message);
            await _context.SaveChangesAsync();
            await TouchFeedAsync(request.FeedId);

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash);

            return Ok(new
            {
                id = message.Id,
                userHash = message.UserHash,
                userName = user?.UserName ?? ShortHash(userHash),
                message = message.Message,
                createdAtUtc = message.CreatedAtUtc,
                isOwn = true
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int feedId, string userHash = "", int? sinceId = null)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            if (!await HasFeedAccessAsync(feedId, userHash))
            {
                return Forbid();
            }

            var query = _context.WorldSharedFeedMessages
                .AsNoTracking()
                .Where(x => x.WorldSharedFeedId == feedId);

            if (sinceId.HasValue)
            {
                query = query.Where(x => x.Id > sinceId.Value);
            }

            var messages = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(50)
                .ToListAsync();

            messages.Reverse();

            var userHashes = messages.Select(x => x.UserHash).Distinct().ToList();
            var users = await _context.WorldAppUser
                .AsNoTracking()
                .Where(x => userHashes.Contains(x.UserHash))
                .ToDictionaryAsync(x => x.UserHash!, x => x.UserName ?? "User");

            var result = messages.Select(m => new
            {
                id = m.Id,
                userHash = m.UserHash,
                userName = users.TryGetValue(m.UserHash, out var name) ? name : ShortHash(m.UserHash),
                message = m.Message,
                createdAtUtc = m.CreatedAtUtc,
                isOwn = string.Equals(m.UserHash, userHash, StringComparison.OrdinalIgnoreCase)
            }).ToList();

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyMealPlans(string userHash = "")
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            var mealPlans = await _context.WorldUserMealPlan
                .AsNoTracking()
                .Where(x => x.UserHash == userHash)
                .OrderByDescending(x => x.CreationTime)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.CreationTime,
                    x.MealPlan,
                    x.Settings
                })
                .ToListAsync();

            var result = mealPlans.Select(plan => {
                int personCount = 1;
                try
                {
                    if (!string.IsNullOrWhiteSpace(plan.Settings))
                    {
                        var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(plan.Settings);
                        if (settings != null && settings.ContainsKey("PersonCount"))
                        {
                            personCount = Convert.ToInt32(settings["PersonCount"]);
                        }
                    }
                }
                catch
                {
                    personCount = 1;
                }

                int recipeCount = 0;
                try
                {
                    if (!string.IsNullOrWhiteSpace(plan.MealPlan))
                    {
                        // Try to deserialize as Dictionary first
                        try
                        {
                            var mealPlanDict = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(plan.MealPlan);
                            recipeCount = mealPlanDict?.Values.SelectMany(x => x).Distinct().Count() ?? 0;
                        }
                        catch
                        {
                            // If that fails, try as a simple array of recipe IDs
                            var mealPlanArray = JsonConvert.DeserializeObject<List<int>>(plan.MealPlan);
                            recipeCount = mealPlanArray?.Distinct().Count() ?? 0;
                        }
                    }
                }
                catch
                {
                    recipeCount = 0;
                }

                return new {
                    plan.Id,
                    plan.Title,
                    CreatedAt = plan.CreationTime,
                    PersonCount = personCount,
                    RecipeCount = recipeCount
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportMealPlan([FromBody] ImportMealPlanRequest request)
        {
            var userHash = ResolveUserHash(request.UserHash ?? string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            if (!await HasFeedAccessAsync(request.FeedId, userHash))
            {
                return Forbid();
            }

            var mealPlan = await _context.WorldUserMealPlan
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.MealPlanId && x.UserHash == userHash);

            if (mealPlan == null)
            {
                return NotFound("Essensplan nicht gefunden.");
            }

            // Parse meal plan data
            var mealPlanData = string.IsNullOrWhiteSpace(mealPlan.MealPlan)
                ? new Dictionary<int, List<int>>()
                : JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(mealPlan.MealPlan);

            // Convert to MealPlanHelperMobile format
            var mealPlanItems = new List<MealPlanHelperMobile>();
            foreach (var entry in mealPlanData)
            {
                var dayIndex = entry.Key;
                foreach (var recipeId in entry.Value)
                {
                    mealPlanItems.Add(new MealPlanHelperMobile
                    {
                        DayIndex = dayIndex,
                        SlotIndex = 0, // Default slot
                        RecipeId = recipeId
                    });
                }
            }

            if (mealPlanItems.Count == 0)
            {
                return BadRequest("Essensplan ist leer.");
            }

            // Get settings
            var settings = string.IsNullOrWhiteSpace(mealPlan.Settings)
                ? new { PersonCount = 1 }
                : JsonConvert.DeserializeObject<dynamic>(mealPlan.Settings);

            var personCount = (int)(settings?.PersonCount ?? 1);
            var recipeIds = mealPlanItems.Select(x => x.RecipeId).Distinct().ToList();

            // Build shopping list
            var shoppingListItems = recipeIds.Any()
                ? await BuildShoppingListItemsAsync(recipeIds, personCount)
                : new List<ShoppingListItem>();

            // Create shared meal plan
            var shareToken = Guid.NewGuid().ToString("N");
            var sharedPlan = new WorldSharedMealPlan
            {
                ShareToken = shareToken,
                UserHash = userHash,
                Title = mealPlan.Title ?? "Importierter Plan",
                PersonCount = personCount,
                MealPlanJson = JsonConvert.SerializeObject(mealPlanItems),
                ShoppingListJson = JsonConvert.SerializeObject(shoppingListItems)
            };

            await _context.WorldSharedMealPlan.AddAsync(sharedPlan);
            await _context.SaveChangesAsync();

            // Add to feed
            var wasAdded = await TryAddSharedFeedItemAsync(
                request.FeedId,
                "mealplan",
                shareToken,
                userHash);

            if (wasAdded)
            {
                await TouchFeedAsync(request.FeedId);
            }

            return Ok(new
            {
                success = true,
                title = sharedPlan.Title,
                recipeCount = recipeIds.Count
            });
        }

        private async Task<List<ShoppingListItem>> BuildShoppingListItemsAsync(List<int> recipeIds, int personCount)
        {
            var recipes = await _context.RecipeBaseData
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                            .ThenInclude(n => n.Group)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                .Where(r => recipeIds.Contains(r.Id))
                .ToListAsync();

            var aggregated = new List<ShoppingListItem>();
            bool totalSalt = false;
            bool totalPepper = false;

            foreach (var recipe in recipes)
            {
                if (recipe.Ingredients == null) continue;

                var basePersonCount = recipe.PersonCount == 0 ? 1 : recipe.PersonCount;
                var scale = (decimal)personCount / basePersonCount;

                foreach (var entry in recipe.Ingredients)
                {
                    var ingredient = entry.Ingredient;
                    var nutrient = ingredient?.IngredientsAndNutrients;
                    var name = nutrient?.Name_DE?.Trim();
                    var groupName = nutrient?.Group?.Name ?? "Sonstiges";
                    var unit = ingredient?.Measure?.Metrics_DE ?? string.Empty;
                    var quantity = ingredient?.Quantity?.Quantitys;

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (string.Equals(name, "salz", StringComparison.OrdinalIgnoreCase))
                    {
                        totalSalt = true;
                        continue;
                    }

                    if (string.Equals(name, "pfeffer", StringComparison.OrdinalIgnoreCase))
                    {
                        totalPepper = true;
                        continue;
                    }

                    aggregated.Add(new ShoppingListItem
                    {
                        GroupName = groupName,
                        IngredientName = name,
                        Unit = unit,
                        Quantity = (decimal)(quantity ?? 0) * scale
                    });
                }
            }

            var summarized = aggregated
                .GroupBy(x => new { x.GroupName, x.IngredientName, x.Unit })
                .Select(g => new ShoppingListItem
                {
                    GroupName = g.Key.GroupName,
                    IngredientName = g.Key.IngredientName,
                    Unit = g.Key.Unit,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();

            if (totalSalt)
            {
                summarized.Add(new ShoppingListItem
                {
                    GroupName = "Extras",
                    IngredientName = "Salz",
                    Unit = string.Empty,
                    Quantity = 0m
                });
            }

            if (totalPepper)
            {
                summarized.Add(new ShoppingListItem
                {
                    GroupName = "Extras",
                    IngredientName = "Pfeffer",
                    Unit = string.Empty,
                    Quantity = 0m
                });
            }

            return summarized;
        }

        public class ImportMealPlanRequest
        {
            public int FeedId { get; set; }
            public int MealPlanId { get; set; }
            public string? UserHash { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserFeeds(string userHash = "")
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            var feedMemberships = await _context.WorldSharedFeedMembers
                .AsNoTracking()
                .Where(x => x.UserHash == userHash)
                .ToListAsync();

            var feedIds = feedMemberships.Select(x => x.WorldSharedFeedId).ToList();

            var feeds = await _context.WorldSharedFeeds
                .AsNoTracking()
                .Where(f => feedIds.Contains(f.Id))
                .Select(f => new
                {
                    f.Id,
                    f.Title,
                    f.OwnerUserHash,
                    MemberCount = _context.WorldSharedFeedMembers.Count(m => m.WorldSharedFeedId == f.Id),
                    ItemCount = _context.WorldSharedFeedItems.Count(i => i.WorldSharedFeedId == f.Id)
                })
                .ToListAsync();

            var result = feeds.Select(f => new
            {
                f.Id,
                f.Title,
                f.MemberCount,
                f.ItemCount,
                IsOwner = string.Equals(f.OwnerUserHash, userHash, StringComparison.OrdinalIgnoreCase)
            })
            .OrderByDescending(x => x.IsOwner)
            .ThenByDescending(x => x.ItemCount)
            .ToList();

            return Ok(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTodo([FromBody] AddTodoRequest request)
        {
            var userHash = ResolveUserHash(request.UserHash ?? string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            if (!await HasFeedAccessAsync(request.FeedId, userHash))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            {
                return BadRequest("Todo-Titel ist leer oder zu lang (max. 200 Zeichen).");
            }

            var maxOrder = await _context.WorldSharedFeedTodos
                .Where(x => x.WorldSharedFeedId == request.FeedId)
                .MaxAsync(x => (int?)x.SortOrder) ?? 0;

            var todo = new WorldSharedFeedTodo
            {
                WorldSharedFeedId = request.FeedId,
                Title = request.Title.Trim(),
                IsCompleted = false,
                CreatedByUserHash = userHash,
                SortOrder = maxOrder + 1
            };

            await _context.WorldSharedFeedTodos.AddAsync(todo);
            await _context.SaveChangesAsync();
            await TouchFeedAsync(request.FeedId);

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash);

            return Ok(new
            {
                id = todo.Id,
                title = todo.Title,
                isCompleted = todo.IsCompleted,
                createdByName = user?.UserName ?? ShortHash(userHash),
                createdAtUtc = todo.CreatedAtUtc
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTodo([FromBody] ToggleTodoRequest request)
        {
            var userHash = ResolveUserHash(request.UserHash ?? string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            var todo = await _context.WorldSharedFeedTodos.FirstOrDefaultAsync(x => x.Id == request.TodoId);
            if (todo == null)
            {
                return NotFound();
            }

            if (!await HasFeedAccessAsync(todo.WorldSharedFeedId, userHash))
            {
                return Forbid();
            }

            todo.IsCompleted = !todo.IsCompleted;
            if (todo.IsCompleted)
            {
                todo.CompletedByUserHash = userHash;
                todo.CompletedAtUtc = DateTime.UtcNow;
            }
            else
            {
                todo.CompletedByUserHash = null;
                todo.CompletedAtUtc = null;
            }

            await _context.SaveChangesAsync();
            await TouchFeedAsync(todo.WorldSharedFeedId);

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash);

            return Ok(new
            {
                id = todo.Id,
                isCompleted = todo.IsCompleted,
                completedByName = todo.IsCompleted ? (user?.UserName ?? ShortHash(userHash)) : null,
                completedAtUtc = todo.CompletedAtUtc
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveFeed(int feedId, string userHash = "")
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            var feed = await _context.WorldSharedFeeds.FirstOrDefaultAsync(x => x.Id == feedId);
            if (feed == null)
            {
                return NotFound();
            }

            // Owner cannot leave their own group
            if (string.Equals(feed.OwnerUserHash, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Als Owner kannst du die Gruppe nicht verlassen. Lösche die Gruppe stattdessen.");
            }

            var membership = await _context.WorldSharedFeedMembers
                .FirstOrDefaultAsync(x => x.WorldSharedFeedId == feedId && x.UserHash == userHash);

            if (membership != null)
            {
                _context.WorldSharedFeedMembers.Remove(membership);
                await _context.SaveChangesAsync();
                await TouchFeedAsync(feedId);
            }

            return RedirectToAction(nameof(FeedLight), new { userHash });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTodo(int todoId, string userHash = "", int? feedId = null)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized();
            }

            var todo = await _context.WorldSharedFeedTodos.FirstOrDefaultAsync(x => x.Id == todoId);
            if (todo == null)
            {
                return NotFound();
            }

            var feed = await _context.WorldSharedFeeds.FirstOrDefaultAsync(x => x.Id == todo.WorldSharedFeedId);
            if (feed == null || !string.Equals(feed.OwnerUserHash, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            _context.WorldSharedFeedTodos.Remove(todo);
            await _context.SaveChangesAsync();
            await TouchFeedAsync(todo.WorldSharedFeedId);

            return RedirectToAction(nameof(FeedLight), new { userHash, feedId = feedId ?? todo.WorldSharedFeedId });
        }

        public class AddTodoRequest
        {
            public int FeedId { get; set; }
            public string? UserHash { get; set; }
            public string Title { get; set; } = string.Empty;
        }

        public class ToggleTodoRequest
        {
            public int TodoId { get; set; }
            public string? UserHash { get; set; }
        }

        public class SendMessageRequest
        {
            public int FeedId { get; set; }
            public string? UserHash { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private static string BuildWorldMiniAppShareUrl(string path)
        {
            return $"https://world.org/mini-app?app_id={WorldMiniAppId}&path={Uri.EscapeDataString(path)}";
        }
    }
}
