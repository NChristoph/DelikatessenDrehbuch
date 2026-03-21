using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Route("WorldMiniApp/Home/{action}")]
    public class RecipeController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobUploadService _blobUpload;
        private readonly ISaveNewRecipeService _saveNewRecipeService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RecipeController> _logger;
        private const int UploadRateLimit = 5;
        private static readonly TimeSpan UploadRateWindow = TimeSpan.FromMinutes(10);

        public RecipeController(
            ApplicationDbContext context,
            IBlobUploadService blobUpload,
            ISaveNewRecipeService saveNewRecipeService,
            IMemoryCache memoryCache,
            ILogger<RecipeController> logger)
        {
            _context = context;
            _blobUpload = blobUpload;
            _saveNewRecipeService = saveNewRecipeService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<IActionResult> Upload(string userHash)
        {
            userHash = ResolveUserHash(userHash);

            var model = new WorldUserPosting()
            {
                ToSelectIngredientsAndNutrients = await _context.IngredientsAndNutrients.Include(x => x.Group).ToListAsync(),
                Measure = await _context.Metrics.ToListAsync(),
                ToSelectKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync()
            };

            return View("~/Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> UploadNewVideoAsync(WorldUserPosting posting, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (!await IsCreatorAllowedAsync(_context, userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }
            if (!TryConsumeUploadSlot(userHash, out var retryAfter))
            {
                if (retryAfter.HasValue)
                {
                    Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }
                return StatusCode(StatusCodes.Status429TooManyRequests, "Upload-Limit erreicht. Bitte später erneut versuchen.");
            }

            var uploadResult = await _blobUpload.UploadContentToBlob(posting.Content);
            SaveNewRecipeModel recipeModel = new()
            {
                Recipes = new Recipes()
                {
                    Name = posting.Title,
                    Category = posting.Recipe.Category,
                    RecipePersonCount = posting.Recipe.PersonCount,
                    ImagePath = uploadResult.SourceUrl
                },
                Querys = posting.Recipe.Preferences,
                IngredientMeasureQuantity = posting.IngredientMeasureQuantity,
            };
            await _saveNewRecipeService.SaveNewAsync(recipeModel, true);
            var recipe = await _context.RecipeBaseData.FirstOrDefaultAsync(r => r.Title == posting.Title);
            posting.CreationTime = DateTime.Now;
            posting.CreatorName = "Avocado";
            posting.CreatorId = userHash;
            posting.Source = uploadResult.SourceUrl;
            posting.ThumbnailUrl = uploadResult.ThumbnailUrl;
            if (recipe != null)
            {
                posting.Recipe = recipe;
                posting.Recipe.PreperationTime = 0;
            }

            await _context.WorldUserPosting.AddAsync(posting);
            await _context.SaveChangesAsync();

            if (recipe != null && posting.SelectedKeywordIds != null && posting.SelectedKeywordIds.Any())
            {
                var keywordLinks = posting.SelectedKeywordIds
                    .Distinct()
                    .Select(keywordId => new RecipeBaseKeyword
                    {
                        RecipeBaseDataId = recipe.Id,
                        KeywordId = keywordId
                    })
                    .ToList();

                await _context.RecipeBaseKeywords.AddRangeAsync(keywordLinks);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
        }

        public async Task<IActionResult> EditRecipe(int postingId, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var posting = await _context.WorldUserPosting
                .IncludeFullPostingRecipe()
                .FirstOrDefaultAsync(x => x.Id == postingId);

            if (posting == null || posting.Recipe == null)
            {
                return NotFound();
            }

            if (!string.Equals(posting.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var model = new EditPostingRecipeViewModel
            {
                PostingId = posting.Id,
                RecipeId = posting.Recipe.Id,
                Title = posting.Title,
                Category = posting.Recipe.Category,
                Preferences = posting.Recipe.Preferences,
                PersonCount = posting.Recipe.PersonCount,
                PreperationTime = posting.Recipe.PreperationTime,
                CurrentImageUrl = posting.ThumbnailUrl ?? posting.Source,
                Ingredients = posting.Recipe.Ingredients?
                    .Where(x => x.Ingredient?.IngredientsAndNutrients != null && x.Ingredient?.Measure != null)
                    .Select(x => new EditPostingIngredientRowViewModel
                    {
                        IngredientId = x.Ingredient.IngredientsAndNutrients.Id,
                        MeasureId = x.Ingredient.Measure.Id,
                        Quantity = x.Ingredient.Quantity?.Quantitys ?? 0
                    })
                    .ToList() ?? new List<EditPostingIngredientRowViewModel>(),
                Steps = posting.Recipe.Steps?
                    .OrderBy(s => s.StepIndex)
                    .Where(s => s.RecipePreperationStep != null)
                    .Select(s => new EditPostingStepRowViewModel
                    {
                        PreperationStepId = s.RecipePreperationStep.Id,
                        StepIndex = s.StepIndex
                    })
                    .ToList() ?? new List<EditPostingStepRowViewModel>(),
                SelectedKeywordIds = posting.Recipe.RecipeKeywords?
                    .Select(k => k.KeywordId)
                    .ToList() ?? new List<int>(),
                AvailableIngredients = await _context.IngredientsAndNutrients.OrderBy(x => x.Name_DE).ToListAsync(),
                AvailableMeasures = await _context.Metrics.OrderBy(x => x.Metriks_DE).ToListAsync(),
                AvailableSteps = await _context.RecipePreperationSteps.ToListAsync(),
                AvailableKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync(),
                IngredientStepJoins = await _context.JoinIngredientPreperationStep
                    .Include(x => x.Preperation)
                    .Include(x => x.Ingredient)
                    .ToListAsync()
            };

            return View("~/Areas/WorldMiniApp/Views/Home/EditRecipe.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> UpsertStep([FromBody] UpsertStepRequest request, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            var isAdmin = User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
            if (!isAdmin && !await IsCreatorAllowedAsync(_context, userHash))
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.De) || string.IsNullOrWhiteSpace(request.En))
            {
                return BadRequest(new { message = "Ungültige Step-Daten." });
            }

            var de = request.De.Trim();
            var en = request.En.Trim();
            var esp = (request.Esp ?? string.Empty).Trim();
            var prt = (request.Prt ?? string.Empty).Trim();
            var phase = request.Phase;
            var equipment = request.Equipment;

            var existing = await _context.RecipePreperationSteps
                .FirstOrDefaultAsync(x => x.Step_DE == de
                    && x.Step_EN == en
                    && x.Step_ESP == esp
                    && x.Step_PRT == prt
                    && x.Phase == phase
                    && x.Equipment == equipment);

            if (existing != null)
            {
                return Json(new { id = existing.Id, reused = true });
            }

            var step = new RecipePreperationSteps
            {
                Step_DE = de,
                Step_EN = en,
                Step_ESP = esp,
                Step_PRT = prt,
                Phase = phase,
                Equipment = equipment
            };

            await _context.RecipePreperationSteps.AddAsync(step);
            await _context.SaveChangesAsync();

            return Json(new { id = step.Id, reused = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRecipe(EditPostingRecipeViewModel model, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var postingToEdit = await _context.WorldUserPosting
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(link => link.Ingredient)
                            .ThenInclude(i => i.Quantity)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(x => x.Id == model.PostingId);

            if (postingToEdit == null || postingToEdit.Recipe == null)
            {
                return NotFound();
            }

            if (!string.Equals(postingToEdit.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            postingToEdit.Title = string.IsNullOrWhiteSpace(model.Title) ? postingToEdit.Title : model.Title.Trim();
            postingToEdit.Recipe.Title = postingToEdit.Title;
            postingToEdit.Recipe.Category = model.Category ?? postingToEdit.Recipe.Category;
            postingToEdit.Recipe.Preferences = model.Preferences ?? postingToEdit.Recipe.Preferences;
            postingToEdit.Recipe.PersonCount = model.PersonCount;
            postingToEdit.Recipe.PreperationTime = model.PreperationTime;

            var existingJoinEntries = postingToEdit.Recipe.Ingredients?.ToList() ?? new List<RecipeJoinIngredientMeasureQuantity>();

            if (existingJoinEntries.Any())
            {
                _context.RecipeJoinIngredientMeasureQuantity.RemoveRange(existingJoinEntries);
                await _context.SaveChangesAsync();
            }

            var parsedRows = ExtractIngredientRowsFromRequest(model, Request.Form);
            var cleanedRows = parsedRows
                .Where(x => x.IngredientId > 0 && x.MeasureId > 0 && x.Quantity > 0)
                .ToList();

            var ingredientIds = cleanedRows.Select(x => x.IngredientId).Distinct().ToList();
            var measureIds = cleanedRows.Select(x => x.MeasureId).Distinct().ToList();

            var ingredientsById = await _context.IngredientsAndNutrients
                .Where(x => ingredientIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var measuresById = await _context.Metrics
                .Where(x => measureIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var row in cleanedRows)
            {
                if (!ingredientsById.TryGetValue(row.IngredientId, out var ingredient))
                {
                    continue;
                }

                if (!measuresById.TryGetValue(row.MeasureId, out var measure))
                {
                    continue;
                }

                var quantity = new Quantity { Quantitys = row.Quantity };
                var ingredientMeasureQuantity = new IngredientMeasureQuantity
                {
                    IngredientsAndNutrients = ingredient,
                    Measure = measure,
                    Quantity = quantity
                };

                var join = new RecipeJoinIngredientMeasureQuantity
                {
                    Recipe = postingToEdit.Recipe,
                    Ingredient = ingredientMeasureQuantity
                };

                await _context.Quantities.AddAsync(quantity);
                await _context.IngredientMeasureQuantity.AddAsync(ingredientMeasureQuantity);
                await _context.RecipeJoinIngredientMeasureQuantity.AddAsync(join);
            }

            if (model.NewContent != null && model.NewContent.Length > 0)
            {
                var uploadResult = await _blobUpload.UploadContentToBlob(model.NewContent);
                postingToEdit.Source = uploadResult.SourceUrl;
                postingToEdit.ThumbnailUrl = uploadResult.ThumbnailUrl;

                var recipeImage = postingToEdit.Recipe.Images?.FirstOrDefault(x => x.WorldAppImage)
                                 ?? postingToEdit.Recipe.Images?.FirstOrDefault();

                if (recipeImage == null)
                {
                    recipeImage = new RecipeBaseDataImage
                    {
                        Recipe = postingToEdit.Recipe,
                        Image = uploadResult.SourceUrl,
                        WorldAppImage = true
                    };
                    await _context.RecipeBaseDataImage.AddAsync(recipeImage);
                }
                else
                {
                    recipeImage.Image = uploadResult.SourceUrl;
                    recipeImage.WorldAppImage = true;
                }
            }

            // Update steps
            var existingSteps = await _context.RecipeJoinPreperationSteps
                .Where(s => s.Recipe.Id == postingToEdit.Recipe.Id)
                .ToListAsync();
            _context.RecipeJoinPreperationSteps.RemoveRange(existingSteps);

            var parsedSteps = ExtractStepRowsFromRequest(Request.Form);
            var stepIds = parsedSteps.Where(s => s.PreperationStepId > 0).Select(s => s.PreperationStepId).ToList();
            var stepEntities = await _context.RecipePreperationSteps
                .Where(s => stepIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            foreach (var stepRow in parsedSteps.Where(s => s.PreperationStepId > 0))
            {
                if (!stepEntities.TryGetValue(stepRow.PreperationStepId, out var stepEntity)) continue;

                var stepJoin = new RecipeJoyinPreperationSteps
                {
                    Recipe = postingToEdit.Recipe,
                    RecipePreperationStep = stepEntity,
                    StepIndex = stepRow.StepIndex
                };
                await _context.RecipeJoinPreperationSteps.AddAsync(stepJoin);
            }

            // Update keywords
            var existingKeywords = await _context.RecipeBaseKeywords
                .Where(k => k.RecipeBaseDataId == postingToEdit.Recipe.Id)
                .ToListAsync();
            _context.RecipeBaseKeywords.RemoveRange(existingKeywords);

            var parsedKeywordIds = ExtractKeywordIdsFromRequest(Request.Form);
            var keywordLinks = parsedKeywordIds
                .Distinct()
                .Select(keywordId => new RecipeBaseKeyword
                {
                    RecipeBaseDataId = postingToEdit.Recipe.Id,
                    KeywordId = keywordId
                })
                .ToList();
            await _context.RecipeBaseKeywords.AddRangeAsync(keywordLinks);

            await _context.SaveChangesAsync();

            return RedirectToAction("MyProfile", "Feed", new { area = "WorldMiniApp", userHash });
        }

        private static List<EditPostingIngredientRowViewModel> ExtractIngredientRowsFromRequest(EditPostingRecipeViewModel model, IFormCollection form)
        {
            var fallback = model.Ingredients ?? new List<EditPostingIngredientRowViewModel>();
            var result = new List<EditPostingIngredientRowViewModel>();

            for (var i = 0; ; i++)
            {
                var ingredientKey = $"Ingredients[{i}].IngredientId";
                var measureKey = $"Ingredients[{i}].MeasureId";
                var quantityKey = $"Ingredients[{i}].Quantity";

                if (!form.ContainsKey(ingredientKey) && !form.ContainsKey(measureKey) && !form.ContainsKey(quantityKey))
                {
                    break;
                }

                _ = int.TryParse(form[ingredientKey].FirstOrDefault(), out var ingredientId);
                _ = int.TryParse(form[measureKey].FirstOrDefault(), out var measureId);

                var rawQuantity = (form[quantityKey].FirstOrDefault() ?? string.Empty).Trim();
                var normalizedQuantity = rawQuantity.Replace(" ", string.Empty).Replace(",", ".");

                var quantityParsed = double.TryParse(normalizedQuantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity)
                                     || double.TryParse(rawQuantity, NumberStyles.Any, CultureInfo.CurrentCulture, out quantity);

                result.Add(new EditPostingIngredientRowViewModel
                {
                    IngredientId = ingredientId,
                    MeasureId = measureId,
                    Quantity = quantityParsed ? quantity : 0
                });
            }

            return result.Any() ? result : fallback;
        }

        private static List<EditPostingStepRowViewModel> ExtractStepRowsFromRequest(IFormCollection form)
        {
            var result = new List<EditPostingStepRowViewModel>();
            for (var i = 0; ; i++)
            {
                var stepIdKey = $"Steps[{i}].PreperationStepId";
                var indexKey = $"Steps[{i}].StepIndex";

                if (!form.ContainsKey(stepIdKey))
                    break;

                _ = int.TryParse(form[stepIdKey].FirstOrDefault(), out var stepId);
                _ = int.TryParse(form[indexKey].FirstOrDefault(), out var stepIndex);

                if (stepId > 0)
                {
                    result.Add(new EditPostingStepRowViewModel
                    {
                        PreperationStepId = stepId,
                        StepIndex = stepIndex > 0 ? stepIndex : i + 1
                    });
                }
            }
            return result;
        }

        private static List<int> ExtractKeywordIdsFromRequest(IFormCollection form)
        {
            var result = new List<int>();
            if (form.TryGetValue("SelectedKeywordIds", out var values))
            {
                foreach (var val in values)
                {
                    if (int.TryParse(val, out var id) && id > 0)
                        result.Add(id);
                }
            }
            return result;
        }

        private bool TryConsumeUploadSlot(string userHash, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            var cacheKey = $"worldminiapp:upload:{userHash}";
            var now = DateTimeOffset.UtcNow;

            var state = _memoryCache.Get<UploadRateState>(cacheKey);
            if (state == null)
            {
                state = new UploadRateState { Count = 1, WindowStart = now };
                _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = UploadRateWindow
                });
                return true;
            }

            if (state.Count >= UploadRateLimit)
            {
                retryAfter = (state.WindowStart + UploadRateWindow) - now;
                _logger.LogWarning("Upload rate limit exceeded for user {UserHash}.", userHash);
                return false;
            }

            state.Count += 1;
            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = UploadRateWindow
            });
            return true;
        }

        private sealed class UploadRateState
        {
            public int Count { get; set; }
            public DateTimeOffset WindowStart { get; set; }
        }
    }
}
