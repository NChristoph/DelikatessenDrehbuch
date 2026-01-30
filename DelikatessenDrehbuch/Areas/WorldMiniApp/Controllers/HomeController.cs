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


namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{

    [Area("WorldMiniApp")]
    public class HomeController : Controller
    {
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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SeedKeywords()
        {
            var keywords = new List<Keyword>
            {
                new() { Word_DE = "Low Carb", Word_EN = "Low Carb", Word_ESP = "Bajo en carbohidratos", Word_PRT = "Baixo carboidrato" },
                new() { Word_DE = "Low Fat", Word_EN = "Low Fat", Word_ESP = "Bajo en grasa", Word_PRT = "Baixo teor de gordura" },
                new() { Word_DE = "Einfach", Word_EN = "Easy", Word_ESP = "Fácil", Word_PRT = "Fácil" },
                new() { Word_DE = "Schnell", Word_EN = "Quick", Word_ESP = "Rápido", Word_PRT = "Rápido" },
                new() { Word_DE = "Gesund", Word_EN = "Healthy", Word_ESP = "Saludable", Word_PRT = "Saudável" },
                new() { Word_DE = "Proteinreich", Word_EN = "High Protein", Word_ESP = "Alto en proteínas", Word_PRT = "Alto teor de proteína" },
                new() { Word_DE = "Kalorienarm", Word_EN = "Low Calorie", Word_ESP = "Bajo en calorías", Word_PRT = "Baixas calorias" },
                new() { Word_DE = "Zuckerfrei", Word_EN = "Sugar Free", Word_ESP = "Sin azúcar", Word_PRT = "Sem açúcar" },
                new() { Word_DE = "Glutenfrei", Word_EN = "Gluten Free", Word_ESP = "Sin gluten", Word_PRT = "Sem glúten" },
                new() { Word_DE = "Laktosefrei", Word_EN = "Lactose Free", Word_ESP = "Sin lactosa", Word_PRT = "Sem lactose" },
                new() { Word_DE = "Vegan", Word_EN = "Vegan", Word_ESP = "Vegano", Word_PRT = "Vegano" },
                new() { Word_DE = "Vegetarisch", Word_EN = "Vegetarian", Word_ESP = "Vegetariano", Word_PRT = "Vegetariano" },
                new() { Word_DE = "High Carb", Word_EN = "High Carb", Word_ESP = "Alto en carbohidratos", Word_PRT = "Alto carboidrato" },
                new() { Word_DE = "Meal Prep", Word_EN = "Meal Prep", Word_ESP = "Meal prep", Word_PRT = "Meal prep" },
                new() { Word_DE = "Familienfreundlich", Word_EN = "Family Friendly", Word_ESP = "Para la familia", Word_PRT = "Para a família" },
                new() { Word_DE = "Kinderfreundlich", Word_EN = "Kid Friendly", Word_ESP = "Para niños", Word_PRT = "Para crianças" },
                new() { Word_DE = "Saisonal", Word_EN = "Seasonal", Word_ESP = "De temporada", Word_PRT = "Sazonal" },
                new() { Word_DE = "Günstig", Word_EN = "Budget", Word_ESP = "Económico", Word_PRT = "Econômico" },
                new() { Word_DE = "Gourmet", Word_EN = "Gourmet", Word_ESP = "Gourmet", Word_PRT = "Gourmet" },
                new() { Word_DE = "Scharf", Word_EN = "Spicy", Word_ESP = "Picante", Word_PRT = "Picante" },
                new() { Word_DE = "Herzhaft", Word_EN = "Savory", Word_ESP = "Salado", Word_PRT = "Salgado" },
                new() { Word_DE = "Süß", Word_EN = "Sweet", Word_ESP = "Dulce", Word_PRT = "Doce" },
                new() { Word_DE = "Frühstück", Word_EN = "Breakfast", Word_ESP = "Desayuno", Word_PRT = "Café da manhã" },
                new() { Word_DE = "Mittagessen", Word_EN = "Lunch", Word_ESP = "Almuerzo", Word_PRT = "Almoço" },
                new() { Word_DE = "Abendessen", Word_EN = "Dinner", Word_ESP = "Cena", Word_PRT = "Jantar" },
                new() { Word_DE = "Snack", Word_EN = "Snack", Word_ESP = "Snack", Word_PRT = "Lanche" },
                new() { Word_DE = "Meal Bowl", Word_EN = "Meal Bowl", Word_ESP = "Bowl", Word_PRT = "Bowl" },
                new() { Word_DE = "One Pot", Word_EN = "One Pot", Word_ESP = "Una olla", Word_PRT = "Panela única" },
                new() { Word_DE = "Ofengericht", Word_EN = "Oven Baked", Word_ESP = "Al horno", Word_PRT = "Assado no forno" },
                new() { Word_DE = "Grill", Word_EN = "Grilled", Word_ESP = "A la parrilla", Word_PRT = "Grelhado" }
            };

            foreach (var keyword in keywords)
            {
                var exists = await _context.Keywords.AnyAsync(k =>
                    k.Word_DE == keyword.Word_DE
                    && k.Word_EN == keyword.Word_EN
                    && k.Word_ESP == keyword.Word_ESP
                    && k.Word_PRT == keyword.Word_PRT);

                if (!exists)
                {
                    await _context.Keywords.AddAsync(keyword);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { count = keywords.Count });
        }

        public async Task<IActionResult> Generator()
        {

            return View(new MiniAppSetupModel());
        }

        //TODO:Splitte das auf hole dir die Creator id un den Namen des posters 

        public async Task<IActionResult> UploadNewVideoAsync(WorldUserPosting posting, string userHash)
        {
            var url = await _blobUpload.UploadContentToBlob(posting.Content);
            SaveNewRecipeModel recipeModel = new()
            {
                Recipes = new Recipes()
                {
                    Name = posting.Title,
                    Category = posting.Recipe.Category,
                    PreparationTime = posting.Recipe.PreperationTime,
                    RecipePersonCount = posting.Recipe.PersonCount,
                    ImagePath = url

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
            posting.Source = url;
            posting.Recipe = recipe;

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

        public async Task<IActionResult> Upload()
        {
            var model = new WorldUserPosting()
            {
                ToSelectIngredientsAndNutrients = await _context.IngredientsAndNutrients.ToListAsync(),
                ToSelectRecipePreperationSteps = await _context.RecipePreperationSteps.ToListAsync(),
                Measure = await _context.Metrics.ToListAsync(),
                ToSelectKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync()
            };

            return View("CreatePosting", model);
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

            var plan = _worldAppMealPlanService.GetMealPlanById(id);
            var settings = JsonConvert.DeserializeObject<MiniAppSetupModel>(plan.Settings);

            List<MealPlanerModel> model = await GetMelplanerModel(plan);

            ViewData["PersonCount"] = settings.PersonCount;
            ViewData["Title"] = plan.Title;

            return View("Generated", model);

        }

   





        public async Task<IActionResult> PersonalityAsync(string userHash)
        {
            ViewData["UserHash"] = userHash;
            var mealPlans = await _worldAppMealPlanService.GetMealPlansByHash(userHash);
            return View(mealPlans);
        }

        public async Task<IActionResult> ViewPlanAsync(int id)
        {
            var plan = _worldAppMealPlanService.GetMealPlanById(id);

            List<MealPlanerModel> model = await GetMelplanerModel(plan);

            return View("Finaly", model);
        }

        public async Task<IActionResult> DeletePlanAsync(string userHash, int id)
        {
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
    }
}
