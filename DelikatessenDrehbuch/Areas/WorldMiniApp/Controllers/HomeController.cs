using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Stripe;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;


namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{

    [Area("WorldMiniApp")]
    public class HomeController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
        private readonly IRecipesService _recipesService;
        private readonly IWorldAppMealPlanService _worldAppMealPlanService;
        private readonly IBlobUploadService _blobUpload;
        private readonly ISaveNewRecipeService _saveNewRecipeService;
        private readonly ApplicationDbContext _context;


        public HomeController(IRecipesService recipesService, IWorldAppMealPlanService worldUserMealPlanService, IBlobUploadService blobUpload, ApplicationDbContext context, ISaveNewRecipeService saveNewRecipeService)
        {
            _recipesService = recipesService;
            _worldAppMealPlanService = worldUserMealPlanService;
            _blobUpload = blobUpload;
            _context = context;
            _saveNewRecipeService = saveNewRecipeService;
        }

        // Die Startseite (Das Menü von oben)
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Generator()
        {

            return View(new MiniAppSetupModel());
        }

        //TODO:Splitte das auf hole dir die Creator id un den Namen des posters 

        public async Task<IActionResult> UploadNewVideoAsync(WorldUserPosting posting, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (!await IsCreatorAllowedAsync(userHash))
            {
                return RedirectToAction("Index");
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
                RecipeJoyinPreperationSteps = posting.RecipePreperationSteps,

            };
            await _saveNewRecipeService.SaveNewAsync(recipeModel, true);
            var recipe = _context.RecipeBaseData.FirstOrDefault(r => r.Title == posting.Title);
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

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Upload(string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (!await IsCreatorAllowedAsync(userHash))
            {
                return RedirectToAction("Index");
            }

            var model = new WorldUserPosting()
            {
                ToSelectIngredientsAndNutrients = await _context.IngredientsAndNutrients.ToListAsync(),
                ToSelectRecipePreperationSteps = await _context.RecipePreperationSteps.ToListAsync(),
                Measure = await _context.Metrics.ToListAsync(),
                ToSelectKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync()
            };

            return View("CreatePosting", model);
        }

        private async Task<bool> IsCreatorAllowedAsync(string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            const string superUserHash = "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839";
            if (userHash == superUserHash)
            {
                return true;
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            return user?.IsVerified == "orb";
        }

        //TODO:Beim andern der rezepte noch auf die preferenz rücksicht nehmen und link zur einkaufsliste teilen
        //lagere das in einen eigenen controller aus

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

        public async Task<IActionResult> Generated(MiniAppSetupModel model, string userHash, string title)
        {
            userHash = ResolveUserHash(userHash);
            ViewData["PersonCount"] = model.PersonCount;
            ViewData["Title"] = title;
            await _worldAppMealPlanService.CheckVerifie(model, userHash, title);

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

            await _worldAppMealPlanService.SaveNewMealPlan(userHash, mealPlan, title);

            return View(mealPlan);
        }

        private async Task<List<MealPlanerModel>> GetMelplanerModel(WorldUserMealPlan plan)
        {

            var indexIds = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(plan.MealPlan);

            List<MealPlanerModel> model = new();
            foreach (var entry in indexIds) // Gehe jeden Tag durch (Key = Tag, Value = Liste IDs)
            {
                int dayIndex = entry.Key;

                foreach (var recipeId in entry.Value) // Gehe jedes Rezept an diesem Tag durch
                {
                    // Finde das passende Rezept-Objekt in der geladenen Liste
                    var recipe = await _context.Recipes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == recipeId);
                    if (recipe != null)
                    {
                        model.Add(new MealPlanerModel
                        {
                            Index = dayIndex,
                            Recipes = recipe
                        });
                        continue;
                    }

                    var baseData = await _context.RecipeBaseData
                        .Include(r => r.Images)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == recipeId);
                    if (baseData == null)
                    {
                        continue;
                    }

                    var baseImage = baseData.Images?.FirstOrDefault()?.Image;
                    model.Add(new MealPlanerModel
                    {
                        Index = dayIndex,
                        Recipes = new Recipes
                        {
                            Id = baseData.Id,
                            Name = baseData.Title,
                            Category = baseData.Category,
                            PreparationTime = baseData.PreperationTime,
                            ImagePath = baseImage
                        },
                        IsBaseData = true
                    });
                }
            }

            return model;
        }

        public async Task<IActionResult> ShowRecipe(int id)
        {
            var model = _context.RecipeBaseData
                .Include(r => r.Images)
                .Include(r => r.Steps)
                    .ThenInclude(s => s.RecipePreperationStep)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                 .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients.Group)
                .FirstOrDefault(r => r.Id == id);
            return View(model);
        }
        public async Task<IActionResult> EditPlan(string userHash, int id)
        {
            userHash = ResolveUserHash(userHash);

            var plan = _worldAppMealPlanService.GetMealPlanById(id);
            var settings = JsonConvert.DeserializeObject<MiniAppSetupModel>(plan.Settings);

            List<MealPlanerModel> model = await GetMelplanerModel(plan);

            ViewData["PersonCount"] = settings.PersonCount;
            ViewData["Title"] = plan.Title;

            return View("Generated", model);

        }

   





        public async Task<IActionResult> PersonalityAsync(string userHash)
        {
            userHash = ResolveUserHash(userHash);
            ViewData["UserHash"] = userHash;
            var mealPlans = await _worldAppMealPlanService.GetMealPlansByHash(userHash);
            return View(mealPlans);
        }

        // TODO: Zutaten-Seeding entfernt (vormals harte Seed-Daten).

        public async Task<IActionResult> ViewPlanAsync(int id)
        {
            var plan = _worldAppMealPlanService.GetMealPlanById(id);
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

            return View("WorldPlan", viewModel);
        }

        public async Task<IActionResult> DeletePlanAsync(string userHash, int id)
        {
            userHash = ResolveUserHash(userHash);
            _worldAppMealPlanService.DeleteMealPlan(id);
            var mealPlans = await _worldAppMealPlanService.GetMealPlansByHash(userHash);
            return View("Personality", mealPlans);
        }

        private async Task<List<Recipes>> GetRandomRecipesByCategory(string category, int count)
        {
            var categoryRecipeIds = StaticData.GetRecipesByCategory(category);
            var randoRecipes = _recipesService.GetRendomRecipesIds(categoryRecipeIds.ToList(), count);
            var recipes = await _recipesService.GetRecipesListByIdsAsync(randoRecipes);
            return recipes;
        }

        // TODO: Zutaten-Seeding entfernt (vormals harte Seed-Daten).

        // 1. ZUFALLS-REZEPT (Würfeln)
        // Gibt nur das HTML für die eine Karte zurück
        public async Task<IActionResult> GetRandomRecipeCard(string category, int dayIndex, string namePrefix)
        {


            var recipe = await GetRandomRecipesByCategory(category, 1); // Methode musst du evtl. in deinem Service haben

          
            // Wir bauen das Model für die Partial View
            var model = new MealPlanerModel
            {
                Index = dayIndex,
                Recipes = recipe.First()
            };
            model.Recipes.ImagePath = FrontendFunctions.GetSmallImagePath(model.Recipes.ImagePath);
            // Daten für die View durchreichen
            ViewData["DayIndex"] = dayIndex;
            ViewData["Category"] = category;
            ViewData["NamePrefix"] = namePrefix;

            return PartialView("_MobileMealCard", model);
        }

        // 2. SUCHE (Ersetzen durch...)
        // Gibt eine Liste von Rezepten zurück, die wir ins Offcanvas laden
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

            return PartialView("_MobileSearchList", recipes);
        }

        public async Task<IActionResult> GetRecipeById(int recipeId, int dayIndex, string namePrefix, string category)
        {
            var model = new MealPlanerModel
            {
                Index = dayIndex,
                Recipes = await _recipesService.GetRecipesFromDbByIdAsync(recipeId)

            };

            model.Recipes.ImagePath = FrontendFunctions.GetSmallImagePath(model.Recipes.ImagePath);
            // Daten für die View durchreichen
            ViewData["DayIndex"] = dayIndex;
            ViewData["Category"] = category;
            ViewData["NamePrefix"] = namePrefix;

            return PartialView("_MobileMealCard", model);
        }


        public class MealPlanHelperMobile
        {
            public int DayIndex { get; set; }      // Der Tag (1, 2, 3...)
            public int RecipeId { get; set; }   // Die ID des gewählten Rezepts
            public int SlotIndex { get; set; }   // Slot (0=Vorspeise, 1=Hauptspeise, 2=Dessert)

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

            foreach (var item in indexIds)
            {
                Recipes recipe = null;
                try
                {
                    recipe = await _recipesService.GetRecipesFromDbByIdAsync(item.RecipeId);
                }
                catch
                {
                    var baseData = await GetRecipeBaseDataByIdAsync(item.RecipeId);
                    if (baseData != null)
                    {
                        usesBaseData = true;
                        if (!baseDataDictionary.ContainsKey(item.DayIndex))
                        {
                            baseDataDictionary[item.DayIndex] = new List<int>();
                        }
                        baseDataDictionary[item.DayIndex].Add(baseData.Id);
                    }
                }

                if (recipe == null)
                {
                    continue;
                }

                MealPlanerModel plan = new()
                {
                    Index = item.DayIndex,
                    Recipes = recipe
                };

                model.Add(plan);
            }

            if (usesBaseData)
            {
                await SaveBaseDataMealPlanAsync(userHash, title, baseDataDictionary, personCount);
                return RedirectToAction("Personality", new { userHash });
            }

            await _worldAppMealPlanService.SaveNewMealPlan(userHash, model, title);
            return View("Finaly", model);
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

        [HttpPost]
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

        [HttpPost]
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

            var existingPlan = _context.WorldUserMealPlan.FirstOrDefault(x => x.UserHash == userHash && x.Title == planTitle);
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
        public IActionResult GetFeedMealPlanDraft(string userHash, string title)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Ok(new { mealPlanJson = string.Empty, personCount = 1, title = string.Empty });
            }

            var planTitle = string.IsNullOrWhiteSpace(title) ? "Feed-Plan" : title.Trim();
            var plan = _context.WorldUserMealPlan.FirstOrDefault(x => x.UserHash == userHash && x.Title == planTitle);
            if (plan == null)
            {
                return Ok(new { mealPlanJson = string.Empty, personCount = 1, title = planTitle });
            }

            var settings = string.IsNullOrWhiteSpace(plan.Settings)
                ? new MiniAppSetupModel { PersonCount = 1 }
                : JsonConvert.DeserializeObject<MiniAppSetupModel>(plan.Settings) ?? new MiniAppSetupModel { PersonCount = 1 };

            return Ok(new { mealPlanJson = plan.MealPlan ?? string.Empty, personCount = settings.PersonCount, title = plan.Title });
        }

        [HttpGet]
        public IActionResult ShareShoppingList(string list)
        {
            ViewData["List"] = list ?? string.Empty;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ShareMealPlan(string token)
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

            return View();
        }

        private string ResolveUserHash(string userHash)
        {
            if (!string.IsNullOrWhiteSpace(userHash))
            {
                HttpContext.Session.SetString(SessionUserHashKey, userHash);
                return userHash;
            }

            return HttpContext.Session.GetString(SessionUserHashKey) ?? string.Empty;
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
                        .ThenInclude(i => i.Measure)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity)
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
                    var unit = ingredient?.Measure?.UnitOfMeasurement ?? string.Empty;
                    var quantity = ingredient?.Quantity?.Quantitys ;

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

            var existingPlan = _context.WorldUserMealPlan.FirstOrDefault(x => x.UserHash == userHash && x.Title == planTitle);
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

        private static string BuildShoppingListText(List<ShoppingListItem> items)
        {
            var grouped = items
                .GroupBy(x => x.GroupName ?? "Sonstiges")
                .OrderBy(g => g.Key);

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

        public class SharedMealPlanRequest
        {
            public string MealPlanJson { get; set; }
            public int PersonCount { get; set; }
            public string Title { get; set; }
            public string? UserHash { get; set; }
        }
    }
}
