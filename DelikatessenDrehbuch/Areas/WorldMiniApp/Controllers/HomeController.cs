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
                    var recipe = await _recipesService.GetRecipesFromDbByIdAsync(recipeId);

                    if (recipe != null)
                    {
                        model.Add(new MealPlanerModel
                        {
                            Index = dayIndex,
                            Recipes = recipe
                        });
                    }
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

        [HttpPost]
        public async Task<IActionResult> SeedWorldIngredientsAndNutrients(string userHash)
        {
            userHash = ResolveUserHash(userHash);
            var existingIngredients = await _context.IngredientsAndNutrients
                .Select(ingredient => ingredient.Name_DE)
                .ToListAsync();
            var existingSet = new HashSet<string>(existingIngredients, StringComparer.OrdinalIgnoreCase);

            var groups = await _context.Group.ToListAsync();
            var groupLookup = groups.ToDictionary(group => group.Name, StringComparer.OrdinalIgnoreCase);
            Group ResolveGroup(string name)
            {
                if (!groupLookup.TryGetValue(name, out var group))
                {
                    group = new Group { Name = name };
                    groupLookup[name] = group;
                    _context.Group.Add(group);
                }

                return group;
            }

            var ingredientsToInsert = GetWorldIngredientSeeds(ResolveGroup)
                .Where(ingredient => !existingSet.Contains(ingredient.Name_DE))
                .ToList();

            if (ingredientsToInsert.Count > 0)
            {
                await _context.IngredientsAndNutrients.AddRangeAsync(ingredientsToInsert);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Personality", new { userHash });
        }

        public async Task<IActionResult> ViewPlanAsync(int id)
        {
            var plan = _worldAppMealPlanService.GetMealPlanById(id);

            List<MealPlanerModel> model = await GetMelplanerModel(plan);

            return View("Finaly", model);
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

        private static List<IngredientsAndNutrients> GetWorldIngredientSeeds(Func<string, Group> resolveGroup)
        {
            return new List<IngredientsAndNutrients>
            {
                new()
                {
                    Name_DE = "Fenchel",
                    Name_EN = "Fennel",
                    Name_PRT = "Funcho",
                    Name_ESP = "Hinojo",
                    Calories_a_100g = 31,
                    Weight_per_piece = 250,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 7.3m,
                    Sugar_a_100g = 3.9m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 1.2m,
                    Fiber_a_100g = 3.1m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Artischocke",
                    Name_EN = "Artichoke",
                    Name_PRT = "Alcachofra",
                    Name_ESP = "Alcachofa",
                    Calories_a_100g = 47,
                    Weight_per_piece = 120,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 10.5m,
                    Sugar_a_100g = 1.0m,
                    Salt_a_100g = 0.09m,
                    Protein_a_100g = 3.3m,
                    Fiber_a_100g = 5.4m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Pak Choi",
                    Name_EN = "Bok choy",
                    Name_PRT = "Acelga chinesa",
                    Name_ESP = "Pak choi",
                    Calories_a_100g = 13,
                    Weight_per_piece = 220,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 2.2m,
                    Sugar_a_100g = 1.2m,
                    Salt_a_100g = 0.06m,
                    Protein_a_100g = 1.5m,
                    Fiber_a_100g = 1.0m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Rosenkohl",
                    Name_EN = "Brussels sprouts",
                    Name_PRT = "Couve-de-bruxelas",
                    Name_ESP = "Coles de Bruselas",
                    Calories_a_100g = 43,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.3m,
                    Saturated_fat_a_100g = 0.06m,
                    Carbohydrates_a_100g = 9.0m,
                    Sugar_a_100g = 2.2m,
                    Salt_a_100g = 0.03m,
                    Protein_a_100g = 3.4m,
                    Fiber_a_100g = 3.8m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Pastinake",
                    Name_EN = "Parsnip",
                    Name_PRT = "Pastinaca",
                    Name_ESP = "Chirivía",
                    Calories_a_100g = 75,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.3m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 18.0m,
                    Sugar_a_100g = 4.8m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 1.2m,
                    Fiber_a_100g = 4.9m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Schwarzwurzel",
                    Name_EN = "Salsify",
                    Name_PRT = "Salsifi",
                    Name_ESP = "Salsifí",
                    Calories_a_100g = 82,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 18.0m,
                    Sugar_a_100g = 2.0m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 3.3m,
                    Fiber_a_100g = 3.3m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Schwarzer Rettich",
                    Name_EN = "Black radish",
                    Name_PRT = "Rabanete preto",
                    Name_ESP = "Rábano negro",
                    Calories_a_100g = 16,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.1m,
                    Saturated_fat_a_100g = 0.02m,
                    Carbohydrates_a_100g = 3.4m,
                    Sugar_a_100g = 1.9m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 0.8m,
                    Fiber_a_100g = 1.6m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Radicchio",
                    Name_EN = "Radicchio",
                    Name_PRT = "Radicchio",
                    Name_ESP = "Radicchio",
                    Calories_a_100g = 23,
                    Weight_per_piece = 300,
                    Fat_a_100g = 0.3m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 4.5m,
                    Sugar_a_100g = 0.6m,
                    Salt_a_100g = 0.04m,
                    Protein_a_100g = 1.4m,
                    Fiber_a_100g = 2.0m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Endivie",
                    Name_EN = "Endive",
                    Name_PRT = "Endívia",
                    Name_ESP = "Endibia",
                    Calories_a_100g = 17,
                    Weight_per_piece = 350,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 3.4m,
                    Sugar_a_100g = 0.3m,
                    Salt_a_100g = 0.06m,
                    Protein_a_100g = 1.3m,
                    Fiber_a_100g = 3.1m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Chicorée",
                    Name_EN = "Belgian endive",
                    Name_PRT = "Chicória belga",
                    Name_ESP = "Endibia belga",
                    Calories_a_100g = 17,
                    Weight_per_piece = 90,
                    Fat_a_100g = 0.1m,
                    Saturated_fat_a_100g = 0.02m,
                    Carbohydrates_a_100g = 3.4m,
                    Sugar_a_100g = 0.3m,
                    Salt_a_100g = 0.06m,
                    Protein_a_100g = 1.2m,
                    Fiber_a_100g = 3.1m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Spitzkohl",
                    Name_EN = "Pointed cabbage",
                    Name_PRT = "Couve pontiaguda",
                    Name_ESP = "Col puntiaguda",
                    Calories_a_100g = 23,
                    Weight_per_piece = 900,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.04m,
                    Carbohydrates_a_100g = 4.7m,
                    Sugar_a_100g = 2.6m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 1.3m,
                    Fiber_a_100g = 2.5m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Okra",
                    Name_EN = "Okra",
                    Name_PRT = "Quiabo",
                    Name_ESP = "Okra",
                    Calories_a_100g = 33,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 7.5m,
                    Sugar_a_100g = 1.5m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 1.9m,
                    Fiber_a_100g = 3.2m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Topinambur",
                    Name_EN = "Jerusalem artichoke",
                    Name_PRT = "Topinambo",
                    Name_ESP = "Topinambur",
                    Calories_a_100g = 73,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 17.4m,
                    Sugar_a_100g = 2.4m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 2.0m,
                    Fiber_a_100g = 1.6m,
                    Group = resolveGroup("Gemüse")
                },
                new()
                {
                    Name_DE = "Rhabarber",
                    Name_EN = "Rhubarb",
                    Name_PRT = "Ruibarbo",
                    Name_ESP = "Ruibarbo",
                    Calories_a_100g = 21,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 4.5m,
                    Sugar_a_100g = 1.1m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 0.9m,
                    Fiber_a_100g = 1.8m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Granatapfel",
                    Name_EN = "Pomegranate",
                    Name_PRT = "Romã",
                    Name_ESP = "Granada",
                    Calories_a_100g = 83,
                    Weight_per_piece = 300,
                    Fat_a_100g = 1.2m,
                    Saturated_fat_a_100g = 0.1m,
                    Carbohydrates_a_100g = 18.7m,
                    Sugar_a_100g = 13.7m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 1.7m,
                    Fiber_a_100g = 4.0m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Kiwi",
                    Name_EN = "Kiwi",
                    Name_PRT = "Kiwi",
                    Name_ESP = "Kiwi",
                    Calories_a_100g = 61,
                    Weight_per_piece = 75,
                    Fat_a_100g = 0.5m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 14.7m,
                    Sugar_a_100g = 9.0m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 1.1m,
                    Fiber_a_100g = 3.0m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Pfirsich",
                    Name_EN = "Peach",
                    Name_PRT = "Pêssego",
                    Name_ESP = "Melocotón",
                    Calories_a_100g = 39,
                    Weight_per_piece = 150,
                    Fat_a_100g = 0.3m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 9.5m,
                    Sugar_a_100g = 8.4m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 0.9m,
                    Fiber_a_100g = 1.5m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Aprikose",
                    Name_EN = "Apricot",
                    Name_PRT = "Damasco",
                    Name_ESP = "Albaricoque",
                    Calories_a_100g = 48,
                    Weight_per_piece = 35,
                    Fat_a_100g = 0.4m,
                    Saturated_fat_a_100g = 0.03m,
                    Carbohydrates_a_100g = 11.1m,
                    Sugar_a_100g = 9.2m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 1.4m,
                    Fiber_a_100g = 2.0m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Brombeeren",
                    Name_EN = "Blackberries",
                    Name_PRT = "Amoras",
                    Name_ESP = "Moras",
                    Calories_a_100g = 43,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.5m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 9.6m,
                    Sugar_a_100g = 4.9m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 1.4m,
                    Fiber_a_100g = 5.3m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Johannisbeeren",
                    Name_EN = "Currants",
                    Name_PRT = "Groselhas",
                    Name_ESP = "Grosellas",
                    Calories_a_100g = 56,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.02m,
                    Carbohydrates_a_100g = 13.8m,
                    Sugar_a_100g = 7.4m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 1.4m,
                    Fiber_a_100g = 4.3m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Cranberries",
                    Name_EN = "Cranberries",
                    Name_PRT = "Oxicocos",
                    Name_ESP = "Arándanos rojos",
                    Calories_a_100g = 46,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.1m,
                    Saturated_fat_a_100g = 0.02m,
                    Carbohydrates_a_100g = 12.2m,
                    Sugar_a_100g = 4.0m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 0.4m,
                    Fiber_a_100g = 4.6m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Maracuja",
                    Name_EN = "Passion fruit",
                    Name_PRT = "Maracujá",
                    Name_ESP = "Maracuyá",
                    Calories_a_100g = 97,
                    Weight_per_piece = 45,
                    Fat_a_100g = 0.7m,
                    Saturated_fat_a_100g = 0.1m,
                    Carbohydrates_a_100g = 23.4m,
                    Sugar_a_100g = 11.2m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 2.2m,
                    Fiber_a_100g = 10.4m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Papaya",
                    Name_EN = "Papaya",
                    Name_PRT = "Mamão",
                    Name_ESP = "Papaya",
                    Calories_a_100g = 43,
                    Weight_per_piece = 600,
                    Fat_a_100g = 0.3m,
                    Saturated_fat_a_100g = 0.1m,
                    Carbohydrates_a_100g = 10.8m,
                    Sugar_a_100g = 7.8m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 0.5m,
                    Fiber_a_100g = 1.7m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Feigen",
                    Name_EN = "Figs",
                    Name_PRT = "Figos",
                    Name_ESP = "Higos",
                    Calories_a_100g = 74,
                    Weight_per_piece = 50,
                    Fat_a_100g = 0.3m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 19.2m,
                    Sugar_a_100g = 16.3m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 0.8m,
                    Fiber_a_100g = 2.9m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Kaki",
                    Name_EN = "Persimmon",
                    Name_PRT = "Caqui",
                    Name_ESP = "Caqui",
                    Calories_a_100g = 70,
                    Weight_per_piece = 200,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.05m,
                    Carbohydrates_a_100g = 18.6m,
                    Sugar_a_100g = 12.5m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 0.6m,
                    Fiber_a_100g = 3.6m,
                    Group = resolveGroup("Obst")
                },
                new()
                {
                    Name_DE = "Haselnüsse",
                    Name_EN = "Hazelnuts",
                    Name_PRT = "Avelãs",
                    Name_ESP = "Avellanas",
                    Calories_a_100g = 628,
                    Weight_per_piece = 0,
                    Fat_a_100g = 60.8m,
                    Saturated_fat_a_100g = 4.5m,
                    Carbohydrates_a_100g = 16.7m,
                    Sugar_a_100g = 4.3m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 14.9m,
                    Fiber_a_100g = 9.7m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Pistazien",
                    Name_EN = "Pistachios",
                    Name_PRT = "Pistaches",
                    Name_ESP = "Pistachos",
                    Calories_a_100g = 562,
                    Weight_per_piece = 0,
                    Fat_a_100g = 45.4m,
                    Saturated_fat_a_100g = 5.6m,
                    Carbohydrates_a_100g = 27.5m,
                    Sugar_a_100g = 7.7m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 20.2m,
                    Fiber_a_100g = 10.3m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Macadamianüsse",
                    Name_EN = "Macadamia nuts",
                    Name_PRT = "Nozes de macadâmia",
                    Name_ESP = "Nueces de macadamia",
                    Calories_a_100g = 718,
                    Weight_per_piece = 0,
                    Fat_a_100g = 75.8m,
                    Saturated_fat_a_100g = 12.1m,
                    Carbohydrates_a_100g = 13.8m,
                    Sugar_a_100g = 4.6m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 7.9m,
                    Fiber_a_100g = 8.6m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Paranüsse",
                    Name_EN = "Brazil nuts",
                    Name_PRT = "Castanhas-do-pará",
                    Name_ESP = "Nueces de Brasil",
                    Calories_a_100g = 656,
                    Weight_per_piece = 0,
                    Fat_a_100g = 66.4m,
                    Saturated_fat_a_100g = 15.1m,
                    Carbohydrates_a_100g = 12.3m,
                    Sugar_a_100g = 2.3m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 14.3m,
                    Fiber_a_100g = 7.5m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Pekannüsse",
                    Name_EN = "Pecans",
                    Name_PRT = "Nozes-pecã",
                    Name_ESP = "Nueces pecanas",
                    Calories_a_100g = 691,
                    Weight_per_piece = 0,
                    Fat_a_100g = 72.0m,
                    Saturated_fat_a_100g = 6.2m,
                    Carbohydrates_a_100g = 13.9m,
                    Sugar_a_100g = 3.9m,
                    Salt_a_100g = 0.0m,
                    Protein_a_100g = 9.2m,
                    Fiber_a_100g = 9.6m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Sonnenblumenkerne",
                    Name_EN = "Sunflower seeds",
                    Name_PRT = "Sementes de girassol",
                    Name_ESP = "Semillas de girasol",
                    Calories_a_100g = 584,
                    Weight_per_piece = 0,
                    Fat_a_100g = 51.5m,
                    Saturated_fat_a_100g = 4.5m,
                    Carbohydrates_a_100g = 20.0m,
                    Sugar_a_100g = 2.6m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 20.8m,
                    Fiber_a_100g = 8.6m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Leinsamen",
                    Name_EN = "Flaxseed",
                    Name_PRT = "Linhaça",
                    Name_ESP = "Linaza",
                    Calories_a_100g = 534,
                    Weight_per_piece = 0,
                    Fat_a_100g = 42.2m,
                    Saturated_fat_a_100g = 3.7m,
                    Carbohydrates_a_100g = 28.9m,
                    Sugar_a_100g = 1.6m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 18.3m,
                    Fiber_a_100g = 27.3m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Sesam",
                    Name_EN = "Sesame seeds",
                    Name_PRT = "Gergelim",
                    Name_ESP = "Sésamo",
                    Calories_a_100g = 573,
                    Weight_per_piece = 0,
                    Fat_a_100g = 49.7m,
                    Saturated_fat_a_100g = 7.0m,
                    Carbohydrates_a_100g = 23.4m,
                    Sugar_a_100g = 0.3m,
                    Salt_a_100g = 0.03m,
                    Protein_a_100g = 17.7m,
                    Fiber_a_100g = 11.8m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Mohn",
                    Name_EN = "Poppy seeds",
                    Name_PRT = "Sementes de papoula",
                    Name_ESP = "Semillas de amapola",
                    Calories_a_100g = 525,
                    Weight_per_piece = 0,
                    Fat_a_100g = 41.6m,
                    Saturated_fat_a_100g = 4.5m,
                    Carbohydrates_a_100g = 28.1m,
                    Sugar_a_100g = 2.9m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 18.0m,
                    Fiber_a_100g = 19.5m,
                    Group = resolveGroup("Nüsse & Samen")
                },
                new()
                {
                    Name_DE = "Buchweizen",
                    Name_EN = "Buckwheat",
                    Name_PRT = "Trigo-sarraceno",
                    Name_ESP = "Trigo sarraceno",
                    Calories_a_100g = 343,
                    Weight_per_piece = 0,
                    Fat_a_100g = 3.4m,
                    Saturated_fat_a_100g = 0.7m,
                    Carbohydrates_a_100g = 71.5m,
                    Sugar_a_100g = 1.1m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 13.3m,
                    Fiber_a_100g = 10.0m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Amaranth",
                    Name_EN = "Amaranth",
                    Name_PRT = "Amaranto",
                    Name_ESP = "Amaranto",
                    Calories_a_100g = 371,
                    Weight_per_piece = 0,
                    Fat_a_100g = 7.0m,
                    Saturated_fat_a_100g = 1.5m,
                    Carbohydrates_a_100g = 65.3m,
                    Sugar_a_100g = 1.7m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 13.6m,
                    Fiber_a_100g = 6.7m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Gerste (Graupen)",
                    Name_EN = "Barley",
                    Name_PRT = "Cevada",
                    Name_ESP = "Cebada",
                    Calories_a_100g = 354,
                    Weight_per_piece = 0,
                    Fat_a_100g = 2.3m,
                    Saturated_fat_a_100g = 0.5m,
                    Carbohydrates_a_100g = 73.5m,
                    Sugar_a_100g = 0.8m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 12.5m,
                    Fiber_a_100g = 17.3m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Roggenmehl",
                    Name_EN = "Rye flour",
                    Name_PRT = "Farinha de centeio",
                    Name_ESP = "Harina de centeno",
                    Calories_a_100g = 335,
                    Weight_per_piece = 0,
                    Fat_a_100g = 1.7m,
                    Saturated_fat_a_100g = 0.3m,
                    Carbohydrates_a_100g = 71.2m,
                    Sugar_a_100g = 1.0m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 9.4m,
                    Fiber_a_100g = 13.2m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Udon-Nudeln",
                    Name_EN = "Udon noodles",
                    Name_PRT = "Macarrão udon",
                    Name_ESP = "Fideos udon",
                    Calories_a_100g = 347,
                    Weight_per_piece = 0,
                    Fat_a_100g = 1.2m,
                    Saturated_fat_a_100g = 0.2m,
                    Carbohydrates_a_100g = 70.4m,
                    Sugar_a_100g = 0.6m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 10.4m,
                    Fiber_a_100g = 3.5m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Soba-Nudeln",
                    Name_EN = "Soba noodles",
                    Name_PRT = "Macarrão soba",
                    Name_ESP = "Fideos soba",
                    Calories_a_100g = 336,
                    Weight_per_piece = 0,
                    Fat_a_100g = 1.4m,
                    Saturated_fat_a_100g = 0.3m,
                    Carbohydrates_a_100g = 68.8m,
                    Sugar_a_100g = 0.9m,
                    Salt_a_100g = 0.02m,
                    Protein_a_100g = 12.0m,
                    Fiber_a_100g = 3.7m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Glasnudeln",
                    Name_EN = "Glass noodles",
                    Name_PRT = "Macarrão de vidro",
                    Name_ESP = "Fideos de cristal",
                    Calories_a_100g = 351,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.1m,
                    Saturated_fat_a_100g = 0.02m,
                    Carbohydrates_a_100g = 86.0m,
                    Sugar_a_100g = 0.2m,
                    Salt_a_100g = 0.03m,
                    Protein_a_100g = 0.2m,
                    Fiber_a_100g = 0.6m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Maisgrieß",
                    Name_EN = "Corn grits",
                    Name_PRT = "Sêmola de milho",
                    Name_ESP = "Sémola de maíz",
                    Calories_a_100g = 362,
                    Weight_per_piece = 0,
                    Fat_a_100g = 3.9m,
                    Saturated_fat_a_100g = 0.6m,
                    Carbohydrates_a_100g = 76.9m,
                    Sugar_a_100g = 0.7m,
                    Salt_a_100g = 0.01m,
                    Protein_a_100g = 8.1m,
                    Fiber_a_100g = 7.3m,
                    Group = resolveGroup("Grundnahrungsmittel")
                },
                new()
                {
                    Name_DE = "Kefir",
                    Name_EN = "Kefir",
                    Name_PRT = "Kefir",
                    Name_ESP = "Kéfir",
                    Calories_a_100g = 60,
                    Weight_per_piece = 0,
                    Fat_a_100g = 3.3m,
                    Saturated_fat_a_100g = 2.1m,
                    Carbohydrates_a_100g = 4.8m,
                    Sugar_a_100g = 4.8m,
                    Salt_a_100g = 0.1m,
                    Protein_a_100g = 3.5m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Milchprodukte")
                },
                new()
                {
                    Name_DE = "Skyr",
                    Name_EN = "Skyr",
                    Name_PRT = "Skyr",
                    Name_ESP = "Skyr",
                    Calories_a_100g = 60,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.2m,
                    Saturated_fat_a_100g = 0.1m,
                    Carbohydrates_a_100g = 3.6m,
                    Sugar_a_100g = 3.6m,
                    Salt_a_100g = 0.12m,
                    Protein_a_100g = 11.0m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Milchprodukte")
                },
                new()
                {
                    Name_DE = "Buttermilch",
                    Name_EN = "Buttermilk",
                    Name_PRT = "Leitelho",
                    Name_ESP = "Suero de mantequilla",
                    Calories_a_100g = 40,
                    Weight_per_piece = 0,
                    Fat_a_100g = 0.8m,
                    Saturated_fat_a_100g = 0.5m,
                    Carbohydrates_a_100g = 4.8m,
                    Sugar_a_100g = 4.8m,
                    Salt_a_100g = 0.1m,
                    Protein_a_100g = 3.4m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Milchprodukte")
                },
                new()
                {
                    Name_DE = "Lammkotelett",
                    Name_EN = "Lamb chop",
                    Name_PRT = "Costeleta de cordeiro",
                    Name_ESP = "Chuleta de cordero",
                    Calories_a_100g = 250,
                    Weight_per_piece = 0,
                    Fat_a_100g = 20.0m,
                    Saturated_fat_a_100g = 9.0m,
                    Carbohydrates_a_100g = 0.0m,
                    Sugar_a_100g = 0.0m,
                    Salt_a_100g = 0.1m,
                    Protein_a_100g = 17.0m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Fleisch")
                },
                new()
                {
                    Name_DE = "Truthahnbrust",
                    Name_EN = "Turkey breast",
                    Name_PRT = "Peito de peru",
                    Name_ESP = "Pechuga de pavo",
                    Calories_a_100g = 135,
                    Weight_per_piece = 0,
                    Fat_a_100g = 1.5m,
                    Saturated_fat_a_100g = 0.4m,
                    Carbohydrates_a_100g = 0.0m,
                    Sugar_a_100g = 0.0m,
                    Salt_a_100g = 0.08m,
                    Protein_a_100g = 29.0m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Fleisch")
                },
                new()
                {
                    Name_DE = "Forelle",
                    Name_EN = "Trout",
                    Name_PRT = "Truta",
                    Name_ESP = "Trucha",
                    Calories_a_100g = 119,
                    Weight_per_piece = 0,
                    Fat_a_100g = 3.5m,
                    Saturated_fat_a_100g = 0.7m,
                    Carbohydrates_a_100g = 0.0m,
                    Sugar_a_100g = 0.0m,
                    Salt_a_100g = 0.1m,
                    Protein_a_100g = 20.5m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Fisch")
                },
                new()
                {
                    Name_DE = "Makrele",
                    Name_EN = "Mackerel",
                    Name_PRT = "Cavala",
                    Name_ESP = "Caballa",
                    Calories_a_100g = 205,
                    Weight_per_piece = 0,
                    Fat_a_100g = 13.9m,
                    Saturated_fat_a_100g = 3.3m,
                    Carbohydrates_a_100g = 0.0m,
                    Sugar_a_100g = 0.0m,
                    Salt_a_100g = 0.1m,
                    Protein_a_100g = 18.6m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Fisch")
                },
                new()
                {
                    Name_DE = "Hering",
                    Name_EN = "Herring",
                    Name_PRT = "Arenque",
                    Name_ESP = "Arenque",
                    Calories_a_100g = 158,
                    Weight_per_piece = 0,
                    Fat_a_100g = 9.0m,
                    Saturated_fat_a_100g = 2.2m,
                    Carbohydrates_a_100g = 0.0m,
                    Sugar_a_100g = 0.0m,
                    Salt_a_100g = 0.12m,
                    Protein_a_100g = 18.0m,
                    Fiber_a_100g = 0.0m,
                    Group = resolveGroup("Fisch")
                }
            };
        }

        // 1. ZUFALLS-REZEPT (Würfeln)
        // Gibt nur das HTML für die eine Karte zurück
        public async Task<IActionResult> GetRandomRecipeCard(string category, int dayIndex, string namePrefix)
        {


            var recipe = await GetRandomRecipesByCategory(category, 1); // Methode musst du evtl. in deinem Service haben

            // Falls dein Service anders funktioniert, hier anpassen!
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

        }
        [HttpGet]
        public async Task<IActionResult> SaveMealPlan(string mealPlanJson, int personCount, string userHash, string title)
        {
            userHash = ResolveUserHash(userHash);
            ViewData["PersonCount"] = personCount;
            var indexIds = JsonConvert.DeserializeObject<List<MealPlanHelperMobile>>(mealPlanJson.ToString());

            List<MealPlanerModel> model = new();

            foreach (var item in indexIds)
            {
                MealPlanerModel plan = new()
                {
                    Index = item.DayIndex,
                    Recipes = await _recipesService.GetRecipesFromDbByIdAsync(item.RecipeId)
                };

                model.Add(plan);
            }
            await _worldAppMealPlanService.SaveNewMealPlan(userHash, model, title);
            return View("Finaly", model);
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
    }
}
