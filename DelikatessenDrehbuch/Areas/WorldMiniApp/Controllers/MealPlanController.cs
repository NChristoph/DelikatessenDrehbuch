using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Route("WorldMiniApp/Home/{action}")]
    public class MealPlanController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IRecipesService _recipesService;
        private readonly IWorldAppMealPlanService _worldAppMealPlanService;
        private readonly ILogger<MealPlanController> _logger;

        public MealPlanController(
            ApplicationDbContext context,
            IRecipesService recipesService,
            IWorldAppMealPlanService worldAppMealPlanService,
            ILogger<MealPlanController> logger)
        {
            _context = context;
            _recipesService = recipesService;
            _worldAppMealPlanService = worldAppMealPlanService;
            _logger = logger;
        }

        public async Task<IActionResult> Generator()
        {
            return View("~/Areas/WorldMiniApp/Views/Home/Generator.cshtml", new MiniAppSetupModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generated(MiniAppSetupModel model, string userHash, string title)
        {
            userHash = ResolveUserHash(userHash);
            ViewData["PersonCount"] = model.PersonCount;
            ViewData["Title"] = title;
            await _worldAppMealPlanService.CheckVerifyAsync(model, userHash, title);

            var recipes = await GetFiltredRecipes(model);

            List<MealPlanerModel> mealPlan = new();

            for (int i = 0; i < recipes.Count; i++)
            {
                mealPlan.Add(new MealPlanerModel
                {
                    Index = i + 1,
                    Recipes = recipes[i]
                });
            }

            await _worldAppMealPlanService.SaveNewMealPlanAsync(userHash, mealPlan, title);

            return View("~/Areas/WorldMiniApp/Views/Home/Generated.cshtml", mealPlan);
        }

        public async Task<IActionResult> EditPlan(string userHash, int id)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var plan = await _worldAppMealPlanService.GetMealPlanByIdAsync(id, userHash);
            if (plan == null)
            {
                return NotFound();
            }

            var settings = string.IsNullOrWhiteSpace(plan.Settings)
                ? new MiniAppSetupModel()
                : JsonConvert.DeserializeObject<MiniAppSetupModel>(plan.Settings) ?? new MiniAppSetupModel();

            List<MealPlanerModel> model = await GetMelplanerModel(plan);

            ViewData["PersonCount"] = settings.PersonCount;
            ViewData["Title"] = plan.Title;

            return View("~/Areas/WorldMiniApp/Views/Home/Generated.cshtml", model);
        }

        public async Task<IActionResult> ViewPlanAsync(int id)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var plan = await _worldAppMealPlanService.GetMealPlanByIdAsync(id, userHash);
            if (plan == null)
            {
                return NotFound();
            }

            var settings = string.IsNullOrWhiteSpace(plan.Settings)
                ? new MiniAppSetupModel { PersonCount = 1 }
                : JsonConvert.DeserializeObject<MiniAppSetupModel>(plan.Settings) ?? new MiniAppSetupModel { PersonCount = 1 };

            List<MealPlanerModel> model = await GetMelplanerModel(plan);
            var baseDataIds = model.Where(x => x.IsBaseData).Select(x => x.Recipes.Id).Distinct().ToList();
            var shoppingListItems = baseDataIds.Any()
                ? await BuildShoppingListItemsAsync(baseDataIds, settings.PersonCount)
                : new List<ShoppingListItem>();

            var viewModel = new WorldMealPlanViewModel
            {
                Title = plan.Title ?? "Mein Plan",
                PersonCount = settings.PersonCount,
                UserHash = plan.UserHash,
                MealPlan = model,
                ShoppingList = shoppingListItems
            };

            return View("~/Areas/WorldMiniApp/Views/Home/WorldPlan.cshtml", viewModel);
        }

        [HttpPost]
        [ActionName("DeletePlan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlanAsync(string userHash, int id)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            await _worldAppMealPlanService.DeleteMealPlanAsync(id, userHash);
            var model = await BuildMyAreaViewModelAsync(userHash);
            ViewData["UserHash"] = userHash;
            return View("~/Areas/WorldMiniApp/Views/Home/Personality.cshtml", model);
        }

        public async Task<IActionResult> PersonalityAsync(string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var model = await BuildMyAreaViewModelAsync(userHash);
            ViewData["UserHash"] = userHash;
            return View("~/Areas/WorldMiniApp/Views/Home/Personality.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> SaveMealPlan(string mealPlanJson, int personCount, string userHash, string title)
        {
            userHash = ResolveUserHash(userHash);
            ViewData["PersonCount"] = personCount;
            var indexIds = JsonConvert.DeserializeObject<List<MealPlanHelperMobile>>(mealPlanJson.ToString());

            List<MealPlanerModel> model = new();
            var baseDataDictionary = new Dictionary<int, List<int>>();
            var usesBaseData = false;

            var allRecipeIds = indexIds.Select(x => x.RecipeId).Distinct().ToList();
            var recipesDict = (await _recipesService.GetRecipesListByIdsAsync(allRecipeIds))
                .ToDictionary(r => r.Id);
            var missingRecipeIds = allRecipeIds.Except(recipesDict.Keys).ToList();
            var baseDataDict = missingRecipeIds.Count > 0
                ? await _context.RecipeBaseData.AsNoTracking()
                    .Where(r => missingRecipeIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id)
                : new Dictionary<int, RecipeBaseData>();

            foreach (var item in indexIds)
            {
                if (recipesDict.TryGetValue(item.RecipeId, out var recipe))
                {
                    model.Add(new MealPlanerModel
                    {
                        Index = item.DayIndex,
                        Recipes = recipe
                    });
                }
                else if (baseDataDict.TryGetValue(item.RecipeId, out var baseData))
                {
                    usesBaseData = true;
                    if (!baseDataDictionary.ContainsKey(item.DayIndex))
                    {
                        baseDataDictionary[item.DayIndex] = new List<int>();
                    }
                    baseDataDictionary[item.DayIndex].Add(baseData.Id);
                }
            }

            if (usesBaseData)
            {
                await SaveBaseDataMealPlanAsync(userHash, title, baseDataDictionary, personCount);
                return RedirectToAction("PersonalityAsync", new { userHash });
            }

            await _worldAppMealPlanService.SaveNewMealPlanAsync(userHash, model, title);
            return View("~/Areas/WorldMiniApp/Views/Home/Finaly.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> GetMealPlanNutritionTotals([FromQuery] List<int> recipeIds, int personCount = 1)
        {
            if (recipeIds == null || recipeIds.Count == 0)
            {
                return Ok(new MealPlanNutritionTotals());
            }

            var totals = await BuildMealPlanNutritionTotalsAsync(recipeIds, Math.Max(1, personCount));
            return Ok(totals);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFeedMealPlanDraft([FromBody] SharedMealPlanRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MealPlanJson))
            {
                return BadRequest("Meal plan fehlt.");
            }

            var userHash = ResolveUserHash(request.UserHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return BadRequest("User Hash fehlt.");
            }

            var planTitle = string.IsNullOrWhiteSpace(request.Title) ? "Feed-Plan" : request.Title.Trim();
            var settings = new MiniAppSetupModel
            {
                PersonCount = request.PersonCount <= 0 ? 1 : request.PersonCount,
                DayCount = 7
            };

            var existingPlan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(x => x.UserHash == userHash && x.Title == planTitle);
            if (existingPlan == null)
            {
                existingPlan = new WorldUserMealPlan
                {
                    UserHash = userHash,
                    Title = planTitle,
                    Settings = JsonConvert.SerializeObject(settings),
                    CreationTime = DateTime.Now
                };
                await _context.WorldUserMealPlan.AddAsync(existingPlan);
            }

            existingPlan.MealPlan = request.MealPlanJson;
            await _context.SaveChangesAsync();

            return Ok(new { saved = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetFeedMealPlanDraft(string userHash, string title)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Ok(new { mealPlanJson = string.Empty, personCount = 1, title = string.Empty });
            }

            var planTitle = string.IsNullOrWhiteSpace(title) ? "Feed-Plan" : title.Trim();
            var plan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(x => x.UserHash == userHash && x.Title == planTitle);
            if (plan == null)
            {
                return Ok(new { mealPlanJson = string.Empty, personCount = 1, title = planTitle });
            }

            var settings = string.IsNullOrWhiteSpace(plan.Settings)
                ? new MiniAppSetupModel { PersonCount = 1 }
                : JsonConvert.DeserializeObject<MiniAppSetupModel>(plan.Settings) ?? new MiniAppSetupModel { PersonCount = 1 };

            return Ok(new { mealPlanJson = plan.MealPlan ?? string.Empty, personCount = settings.PersonCount, title = plan.Title });
        }

        public async Task<IActionResult> GetRandomRecipeCard(string category, int dayIndex, string namePrefix)
        {
            var recipe = await GetRandomRecipesByCategory(category, 1);

            var model = new MealPlanerModel
            {
                Index = dayIndex,
                Recipes = recipe.First()
            };
            model.Recipes.ImagePath = FrontendFunctions.GetSmallImagePath(model.Recipes.ImagePath);
            ViewData["DayIndex"] = dayIndex;
            ViewData["Category"] = category;
            ViewData["NamePrefix"] = namePrefix;

            return PartialView("~/Areas/WorldMiniApp/Views/Home/_MobileMealCard.cshtml", model);
        }

        public async Task<IActionResult> GetSearchList(string category, int dayIndex, string namePrefix)
        {
            var recipes = await GetRandomRecipesByCategory(category, 20);

            foreach (var recipe in recipes)
            {
                recipe.ImagePath = FrontendFunctions.GetSmallImagePath(recipe.ImagePath);
            }

            ViewBag.DayIndex = dayIndex;
            ViewBag.NamePrefix = namePrefix;
            ViewBag.Category = category;

            return PartialView("~/Areas/WorldMiniApp/Views/Home/_MobileSearchList.cshtml", recipes);
        }

        public async Task<IActionResult> GetRecipeById(int recipeId, int dayIndex, string namePrefix, string category)
        {
            var model = new MealPlanerModel
            {
                Index = dayIndex,
                Recipes = await _recipesService.GetRecipesFromDbByIdAsync(recipeId)
            };

            model.Recipes.ImagePath = FrontendFunctions.GetSmallImagePath(model.Recipes.ImagePath);
            ViewData["DayIndex"] = dayIndex;
            ViewData["Category"] = category;
            ViewData["NamePrefix"] = namePrefix;

            return PartialView("~/Areas/WorldMiniApp/Views/Home/_MobileMealCard.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSharedMealPlan([FromBody] SharedMealPlanRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MealPlanJson))
            {
                return BadRequest("Meal plan fehlt.");
            }

            var mealPlan = JsonConvert.DeserializeObject<List<MealPlanHelperMobile>>(request.MealPlanJson);
            if (mealPlan == null || mealPlan.Count == 0)
            {
                return BadRequest("Meal plan ist leer.");
            }

            var recipeIds = mealPlan.Select(x => x.RecipeId).Distinct().ToList();
            var items = await BuildShoppingListItemsAsync(recipeIds, request.PersonCount);

            var shareToken = Guid.NewGuid().ToString("N");
            var sharedPlan = new WorldSharedMealPlan
            {
                ShareToken = shareToken,
                UserHash = request.UserHash,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Mein Wochenplan" : request.Title.Trim(),
                PersonCount = request.PersonCount <= 0 ? 1 : request.PersonCount,
                MealPlanJson = request.MealPlanJson,
                ShoppingListJson = JsonConvert.SerializeObject(items)
            };

            await _context.WorldSharedMealPlan.AddAsync(sharedPlan);
            await _context.SaveChangesAsync();

            var shareUrl = Url.Action("ShareMealPlan", "Home", new { area = "WorldMiniApp", token = shareToken }, Request.Scheme);
            return Ok(new { shareUrl });
        }

        [HttpGet]
        public async Task<IActionResult> ShareMealPlan(string token, bool? direct)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return NotFound();
            }

            var sharedPlan = await _context.WorldSharedMealPlan.FirstOrDefaultAsync(x => x.ShareToken == token);
            if (sharedPlan == null)
            {
                return NotFound();
            }

            // Wenn nicht mit direct=true UND nicht in World App → Landing Page zeigen
            var isWorldApp = IsWorldAppRequest();
            if (direct != true && !isWorldApp)
            {
                ViewData["ShareType"] = "meal-plan";
                ViewData["Token"] = token;
                ViewData["Title"] = sharedPlan.Title ?? "Geteilter Essensplan";
                ViewData["Description"] = $"Öffne diesen Essensplan in der World App ({sharedPlan.PersonCount} Person(en), {sharedPlan.MealPlanJson?.Count(c => c == '{') ?? 0} Rezepte).";
                ViewData["TargetPath"] = $"/WorldMiniApp/MealPlan/ShareMealPlan?token={token}&direct=true";
                return View("~/Areas/WorldMiniApp/Views/Home/SharedLinkLanding.cshtml");
            }

            var mealPlan = JsonConvert.DeserializeObject<List<MealPlanHelperMobile>>(sharedPlan.MealPlanJson) ?? new();
            var recipeIds = mealPlan.Select(x => x.RecipeId).Distinct().ToList();
            var recipeNames = await _context.RecipeBaseData
                .Where(r => recipeIds.Contains(r.Id))
                .Select(r => r.Title)
                .ToListAsync();

            var items = string.IsNullOrWhiteSpace(sharedPlan.ShoppingListJson)
                ? new List<ShoppingListItem>()
                : JsonConvert.DeserializeObject<List<ShoppingListItem>>(sharedPlan.ShoppingListJson) ?? new();

            ViewData["Title"] = sharedPlan.Title;
            ViewData["PersonCount"] = sharedPlan.PersonCount;
            ViewData["RecipeNames"] = recipeNames;
            ViewData["ShoppingListText"] = BuildShoppingListText(items);

            return View("~/Areas/WorldMiniApp/Views/Home/ShareMealPlan.cshtml");
        }

        private bool IsWorldAppRequest()
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            return userAgent.Contains("WorldApp", StringComparison.OrdinalIgnoreCase) ||
                   userAgent.Contains("MiniKit", StringComparison.OrdinalIgnoreCase) ||
                   userAgent.Contains("Worldcoin", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public IActionResult ShareShoppingList(string list)
        {
            ViewData["List"] = list ?? string.Empty;
            return View("~/Areas/WorldMiniApp/Views/Home/ShareShoppingList.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> GetShoppingListText([FromQuery] List<int> recipeIds, int personCount = 1)
        {
            if (recipeIds == null || recipeIds.Count == 0)
            {
                return Ok(new { text = string.Empty });
            }

            var items = await BuildShoppingListItemsAsync(recipeIds, personCount);
            var text = BuildShoppingListText(items);
            return Ok(new { text });
        }

        #region Private Helpers

        private async Task<List<Recipes>> GetFiltredRecipes(MiniAppSetupModel model)
        {
            var ids = new List<int>();
            if (model.DietType == "Alles")
                ids = StaticData.MainMeals;
            if (model.DietType == "Vegetarisch")
                ids = StaticData.VegetarianRecipeIds;
            if (model.DietType == "Vegan")
                ids = StaticData.VeganRecipeIds;

            if (model.Filters.Contains("CookingTimeOne"))
                ids = StaticData.CookingTimeOne.Intersect(ids).ToList();

            ids = ids.Intersect(StaticData.MainMeals).ToList();

            var ran = _recipesService.GetRendomRecipesIds(ids, model.DayCount);

            return await _recipesService.GetRecipesListByIdsAsync(ran);
        }

        private async Task<List<Recipes>> GetRandomRecipesByCategory(string category, int count)
        {
            var categoryRecipeIds = StaticData.GetRecipesByCategory(category);
            var randoRecipes = _recipesService.GetRendomRecipesIds(categoryRecipeIds.ToList(), count);
            var recipes = await _recipesService.GetRecipesListByIdsAsync(randoRecipes);
            return recipes;
        }

        private async Task<List<MealPlanerModel>> GetMelplanerModel(WorldUserMealPlan plan)
        {
            var indexIds = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(plan.MealPlan);
            if (indexIds == null) return new();

            var allIds = indexIds.Values.SelectMany(x => x).Distinct().ToList();

            var recipesDict = await _context.Recipes
                .AsNoTracking()
                .Where(r => allIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id);

            var missingIds = allIds.Except(recipesDict.Keys).ToList();
            var baseDataDict = missingIds.Count > 0
                ? await _context.RecipeBaseData.Include(r => r.Images).AsNoTracking()
                    .Where(r => missingIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id)
                : new Dictionary<int, RecipeBaseData>();

            List<MealPlanerModel> model = new();
            foreach (var entry in indexIds)
            {
                foreach (var recipeId in entry.Value)
                {
                    if (recipesDict.TryGetValue(recipeId, out var recipe))
                    {
                        model.Add(new MealPlanerModel { Index = entry.Key, Recipes = recipe });
                        continue;
                    }
                    if (baseDataDict.TryGetValue(recipeId, out var baseData))
                    {
                        var baseImage = baseData.Images?.FirstOrDefault()?.Image;
                        model.Add(new MealPlanerModel
                        {
                            Index = entry.Key,
                            Recipes = new Recipes { Id = baseData.Id, Name = baseData.Title, Category = baseData.Category, PreparationTime = baseData.PreparationTime, ImagePath = baseImage },
                            IsBaseData = true
                        });
                    }
                }
            }
            return model;
        }

        private async Task<MyAreaViewModel> BuildMyAreaViewModelAsync(string userHash)
        {
            var mealPlans = await _worldAppMealPlanService.GetMealPlansByHashAsync(userHash);
            var purchases = await _context.MealPlanPurchases
                .Include(x => x.Listing)
                .Where(x => x.BuyerHash == userHash)
                .OrderByDescending(x => x.PurchasedAt)
                .ToListAsync();

            var purchasedMealPlanIds = purchases
                .Select(x => x.CreatedMealPlanId)
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet();

            return new MyAreaViewModel
            {
                UserHash = userHash,
                CreatedMealPlans = mealPlans
                    .Where(x => !purchasedMealPlanIds.Contains(x.Id))
                    .OrderByDescending(x => x.CreationTime)
                    .ToList(),
                Purchases = purchases
            };
        }

        private async Task<List<ShoppingListItem>> BuildShoppingListItemsAsync(List<int> recipeIds, int personCount)
        {
            var recipes = await _context.RecipeBaseData
                .IncludeIngredients()
                .Where(r => recipeIds.Contains(r.Id))
                .ToListAsync();

            var aggregated = new List<ShoppingListItem>();
            bool totalSalt = false;
            bool totalPepper = false;

            foreach (var recipe in recipes)
            {
                if (recipe.Ingredients == null)
                {
                    continue;
                }

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

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

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

        private async Task<MealPlanNutritionTotals> BuildMealPlanNutritionTotalsAsync(List<int> recipeIds, int personCount)
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
                .ToListAsync();

            var totals = new MealPlanNutritionTotals();

            foreach (var recipe in recipes)
            {
                if (recipe.Ingredients == null)
                {
                    continue;
                }

                var basePersonCount = recipe.PersonCount == 0 ? 1 : recipe.PersonCount;
                var scale = (decimal)personCount / basePersonCount;

                foreach (var entry in recipe.Ingredients)
                {
                    var ingredient = entry.Ingredient;
                    var nutrient = ingredient?.IngredientsAndNutrients;
                    if (nutrient == null)
                    {
                        continue;
                    }

                    var quantity = ingredient.Quantity?.Quantitys ?? 0;
                    var unit = ingredient.Measure?.Metrics_DE ?? string.Empty;
                    var grams = (decimal)quantity;

                    if (string.Equals(unit, "Stk.", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(unit, "Stück", StringComparison.OrdinalIgnoreCase))
                    {
                        var weightPerPiece = nutrient.Weight_per_piece > 0 ? nutrient.Weight_per_piece : 0;
                        grams = (decimal)weightPerPiece * (decimal)quantity;
                    }

                    grams *= scale;
                    if (grams <= 0)
                    {
                        continue;
                    }

                    totals.Calories += ((decimal)nutrient.Calories_a_100g * grams) / 100m;
                    totals.Fat += (nutrient.Fat_a_100g * grams) / 100m;
                    totals.SaturatedFat += (nutrient.Saturated_fat_a_100g * grams) / 100m;
                    totals.Carbohydrates += (nutrient.Carbohydrates_a_100g * grams) / 100m;
                    totals.Sugar += (nutrient.Sugar_a_100g * grams) / 100m;
                    totals.Salt += (nutrient.Salt_a_100g * grams) / 100m;
                    totals.Protein += (nutrient.Protein_a_100g * grams) / 100m;
                    totals.Fiber += (nutrient.Fiber_a_100g * grams) / 100m;
                }
            }

            totals.Calories = Math.Round(totals.Calories, 0);
            totals.Fat = Math.Round(totals.Fat, 1);
            totals.SaturatedFat = Math.Round(totals.SaturatedFat, 1);
            totals.Carbohydrates = Math.Round(totals.Carbohydrates, 1);
            totals.Sugar = Math.Round(totals.Sugar, 1);
            totals.Salt = Math.Round(totals.Salt, 1);
            totals.Protein = Math.Round(totals.Protein, 1);
            totals.Fiber = Math.Round(totals.Fiber, 1);

            return totals;
        }

        private async Task<RecipeBaseData?> GetRecipeBaseDataByIdAsync(int id)
        {
            return await _context.RecipeBaseData.FirstOrDefaultAsync(r => r.Id == id);
        }

        private async Task SaveBaseDataMealPlanAsync(string userHash, string title, Dictionary<int, List<int>> baseDataDictionary, int personCount)
        {
            var planTitle = string.IsNullOrWhiteSpace(title) ? "Feed-Plan" : title.Trim();
            var settings = new MiniAppSetupModel
            {
                PersonCount = personCount <= 0 ? 1 : personCount,
                DayCount = 7
            };

            var existingPlan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(x => x.UserHash == userHash && x.Title == planTitle);
            if (existingPlan == null)
            {
                existingPlan = new WorldUserMealPlan
                {
                    UserHash = userHash,
                    Title = planTitle,
                    Settings = JsonConvert.SerializeObject(settings),
                    CreationTime = DateTime.Now
                };
                await _context.WorldUserMealPlan.AddAsync(existingPlan);
            }

            existingPlan.MealPlan = JsonConvert.SerializeObject(baseDataDictionary);
            await _context.SaveChangesAsync();
        }

        // Supermarkt-Bereich-Mapping: Zutat-Gruppen → Supermarkt-Abteilung + Sortierung
        private static readonly Dictionary<string, (string Aisle, int Order)> AisleMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gemüse"]        = ("🥬 Obst & Gemüse", 1),
            ["Obst"]          = ("🥬 Obst & Gemüse", 1),
            ["Milchprodukte"] = ("🧊 Kühlregal", 2),
            ["Milch"]         = ("🧊 Kühlregal", 2),
            ["Fleisch"]       = ("🥩 Frischetheke", 3),
            ["Fisch"]         = ("🥩 Frischetheke", 3),
            ["Getreide"]      = ("🌾 Trockenware", 4),
            ["Nüsse"]         = ("🌾 Trockenware", 4),
            ["Gewürze"]       = ("🧂 Gewürze & Extras", 5),
            ["Extras"]        = ("🧂 Gewürze & Extras", 5),
            ["Sonstige"]      = ("📦 Sonstiges", 6),
            ["Sonstiges"]     = ("📦 Sonstiges", 6),
        };

        private static (string Aisle, int Order) GetAisle(string groupName)
        {
            if (!string.IsNullOrEmpty(groupName) && AisleMap.TryGetValue(groupName, out var mapped))
                return mapped;
            return ("📦 Sonstiges", 6);
        }

        private static string BuildShoppingListText(List<ShoppingListItem> items)
        {
            var grouped = items
                .GroupBy(x => GetAisle(x.GroupName ?? "Sonstiges").Aisle)
                .OrderBy(g => GetAisle(g.First().GroupName ?? "Sonstiges").Order)
                .ThenBy(g => g.Key);

            var builder = new System.Text.StringBuilder();
            foreach (var group in grouped)
            {
                builder.AppendLine($"{group.Key}:");
                foreach (var item in group.OrderBy(x => x.IngredientName))
                {
                    if (string.IsNullOrWhiteSpace(item.Unit))
                    {
                        builder.AppendLine($"- {item.IngredientName}");
                    }
                    else
                    {
                        builder.AppendLine($"- {item.Quantity:0.##} {item.Unit} {item.IngredientName}");
                    }
                }
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        #endregion

        #region Shared Shopping List

        public class SharedShoppingListRequest
        {
            public string ItemsJson { get; set; }
            public string UserHash { get; set; }
        }

        public class UpdateCheckedRequest
        {
            public string Token { get; set; }
            public List<int> Checked { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSharedShoppingList([FromForm] SharedShoppingListRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemsJson))
            {
                return BadRequest("Einkaufsliste fehlt.");
            }

            var shareToken = Guid.NewGuid().ToString("N");
            var sharedList = new WorldSharedShoppingList
            {
                ShareToken = shareToken,
                UserHash = request.UserHash ?? "",
                ItemsJson = request.ItemsJson,
                CheckedJson = "[]"
            };

            await _context.WorldSharedShoppingList.AddAsync(sharedList);
            await _context.SaveChangesAsync();

            var shareUrl = Url.Action("SharedShoppingList", "Home", new { area = "WorldMiniApp", token = shareToken }, Request.Scheme);
            return Ok(new { shareUrl, token = shareToken });
        }

        [HttpGet]
        public async Task<IActionResult> GetSharedListState(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest();

            var list = await _context.WorldSharedShoppingList.FirstOrDefaultAsync(x => x.ShareToken == token);
            if (list == null)
                return NotFound();

            return Ok(new
            {
                items = list.ItemsJson,
                @checked = list.CheckedJson
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSharedListChecked([FromBody] UpdateCheckedRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
                return BadRequest();

            var list = await _context.WorldSharedShoppingList.FirstOrDefaultAsync(x => x.ShareToken == request.Token);
            if (list == null)
                return NotFound();

            list.CheckedJson = JsonConvert.SerializeObject(request.Checked ?? new List<int>());
            await _context.SaveChangesAsync();

            return Ok(new { ok = true });
        }

        #endregion
    }
}
