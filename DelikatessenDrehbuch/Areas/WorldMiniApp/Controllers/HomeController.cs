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
        public async Task<IActionResult> SeedWorldPreparationSteps(string userHash)
        {
            userHash = ResolveUserHash(userHash);
            var existingSteps = await _context.RecipePreperationSteps
                .Select(step => step.Step_DE)
                .ToListAsync();
            var existingSet = new HashSet<string>(existingSteps, StringComparer.OrdinalIgnoreCase);
            var stepsToInsert = GetWorldPreparationStepSeeds()
                .Where(step => !existingSet.Contains(step.Step_DE))
                .ToList();

            if (stepsToInsert.Count > 0)
            {
                await _context.RecipePreperationSteps.AddRangeAsync(stepsToInsert);
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

        private static List<RecipePreperationSteps> GetWorldPreparationStepSeeds()
        {
            return new List<RecipePreperationSteps>
            {
                new() { Step_DE = "Bringe einen großen Topf Salzwasser zum Kochen.", Step_EN = "Bring a large pot of salted water to a boil.", Step_PRT = "Leve uma panela grande com água e sal para ferver.", Step_ESP = "Lleva a ebullición una olla grande con agua salada." },
                new() { Step_DE = "Gib die Pasta ins kochende Wasser und rühre um.", Step_EN = "Add the pasta to the boiling water and stir.", Step_PRT = "Adicione a massa à água fervente e mexa.", Step_ESP = "Añade la pasta al agua hirviendo y remueve." },
                new() { Step_DE = "Koche die Pasta bissfest nach Packungsangabe.", Step_EN = "Cook the pasta al dente according to the package instructions.", Step_PRT = "Cozinhe a massa al dente conforme a embalagem.", Step_ESP = "Cocina la pasta al dente según el paquete." },
                new() { Step_DE = "Heb vor dem Abgießen eine Kelle Nudelwasser auf.", Step_EN = "Reserve a ladle of pasta water before draining.", Step_PRT = "Reserve uma concha da água da massa antes de escorrer.", Step_ESP = "Reserva un cucharón del agua de la pasta antes de escurrir." },
                new() { Step_DE = "Gieße die Pasta ab und lasse sie kurz ausdampfen.", Step_EN = "Drain the pasta and let it steam off briefly.", Step_PRT = "Escorra a massa e deixe evaporar por instantes.", Step_ESP = "Escurre la pasta y deja que se evapore un momento." },
                new() { Step_DE = "Erhitze Olivenöl in einer Pfanne für die Pastasauce.", Step_EN = "Heat olive oil in a pan for the pasta sauce.", Step_PRT = "Aqueça azeite numa frigideira para o molho.", Step_ESP = "Calienta aceite de oliva en una sartén para la salsa." },
                new() { Step_DE = "Schneide die Zwiebel in feine Würfel für die Sauce.", Step_EN = "Dice the onion finely for the sauce.", Step_PRT = "Pique a cebola finamente para o molho.", Step_ESP = "Pica la cebolla finamente para la salsa." },
                new() { Step_DE = "Schwitze die Zwiebelwürfel glasig an.", Step_EN = "Sauté the diced onion until translucent.", Step_PRT = "Refogue a cebola picada até ficar translúcida.", Step_ESP = "Sofríe la cebolla hasta que quede transparente." },
                new() { Step_DE = "Gib fein gehackten Knoblauch dazu und rühre kurz um.", Step_EN = "Add finely chopped garlic and stir briefly.", Step_PRT = "Adicione alho picado e mexa rapidamente.", Step_ESP = "Añade ajo picado y remueve brevemente." },
                new() { Step_DE = "Rühre Tomatenmark ein und röste es kurz an.", Step_EN = "Stir in tomato paste and toast it briefly.", Step_PRT = "Misture o extrato de tomate e toste rapidamente.", Step_ESP = "Incorpora el concentrado de tomate y tuéstalo un momento." },
                new() { Step_DE = "Lösche mit passierten Tomaten ab.", Step_EN = "Deglaze with crushed tomatoes.", Step_PRT = "Deglace com tomate triturado.", Step_ESP = "Desglasa con tomate triturado." },
                new() { Step_DE = "Würze die Tomatensauce mit Salz, Pfeffer und Basilikum.", Step_EN = "Season the tomato sauce with salt, pepper, and basil.", Step_PRT = "Tempere o molho de tomate com sal, pimenta e manjericão.", Step_ESP = "Sazona la salsa de tomate con sal, pimienta y albahaca." },
                new() { Step_DE = "Lass die Sauce bei kleiner Hitze 15 Minuten köcheln.", Step_EN = "Simmer the sauce on low heat for 15 minutes.", Step_PRT = "Cozinhe o molho em fogo baixo por 15 minutos.", Step_ESP = "Cocina la salsa a fuego bajo durante 15 minutos." },
                new() { Step_DE = "Rühre die Sauce cremig glatt, falls nötig.", Step_EN = "Blend the sauce smooth if needed.", Step_PRT = "Bata o molho até ficar liso, se necessário.", Step_ESP = "Tritura la salsa si es necesario." },
                new() { Step_DE = "Reibe den Käse frisch für die Pasta.", Step_EN = "Grate the cheese fresh for the pasta.", Step_PRT = "Rale o queijo fresco para a massa.", Step_ESP = "Ralla el queso fresco para la pasta." },
                new() { Step_DE = "Verrühre Sahne mit geriebenem Käse für eine cremige Sauce.", Step_EN = "Stir cream with grated cheese for a creamy sauce.", Step_PRT = "Misture creme com queijo ralado para um molho cremoso.", Step_ESP = "Mezcla nata con queso rallado para una salsa cremosa." },
                new() { Step_DE = "Gib die Sauce zur Pasta und mische alles gründlich.", Step_EN = "Add the sauce to the pasta and mix well.", Step_PRT = "Adicione o molho à massa e misture bem.", Step_ESP = "Añade la salsa a la pasta y mezcla bien." },
                new() { Step_DE = "Füge etwas Nudelwasser hinzu, um die Sauce zu binden.", Step_EN = "Add a little pasta water to bind the sauce.", Step_PRT = "Adicione um pouco da água da massa para ligar o molho.", Step_ESP = "Añade un poco de agua de la pasta para ligar la salsa." },
                new() { Step_DE = "Schmecke die Pasta mit frischen Kräutern ab.", Step_EN = "Season the pasta with fresh herbs.", Step_PRT = "Tempere a massa com ervas frescas.", Step_ESP = "Sazona la pasta con hierbas frescas." },
                new() { Step_DE = "Halbiere Kirschtomaten und gib sie zur Sauce.", Step_EN = "Halve cherry tomatoes and add them to the sauce.", Step_PRT = "Corte os tomates-cereja ao meio e junte ao molho.", Step_ESP = "Parte los tomates cherry por la mitad y añádelos a la salsa." },
                new() { Step_DE = "Röste Pinienkerne in einer trockenen Pfanne goldbraun.", Step_EN = "Toast pine nuts in a dry pan until golden.", Step_PRT = "Torre pinhões numa frigideira seca até dourar.", Step_ESP = "Tuesta piñones en una sartén seca hasta dorar." },
                new() { Step_DE = "Streue die gerösteten Pinienkerne über die Pasta.", Step_EN = "Sprinkle the toasted pine nuts over the pasta.", Step_PRT = "Polvilhe os pinhões torrados sobre a massa.", Step_ESP = "Espolvorea los piñones tostados sobre la pasta." },
                new() { Step_DE = "Schneide Zucchini in halbe Scheiben für eine Pasta-Pfanne.", Step_EN = "Slice zucchini into half-moons for a pasta skillet.", Step_PRT = "Corte a abobrinha em meias-luas para a massa.", Step_ESP = "Corta el calabacín en medias lunas para la pasta." },
                new() { Step_DE = "Brate die Zucchini kurz an, bis sie Farbe bekommt.", Step_EN = "Sauté the zucchini briefly until it colors.", Step_PRT = "Salteie a abobrinha rapidamente até dourar.", Step_ESP = "Saltea el calabacín brevemente hasta que tome color." },
                new() { Step_DE = "Füge Spinat hinzu und lass ihn zusammenfallen.", Step_EN = "Add spinach and let it wilt.", Step_PRT = "Adicione espinafre e deixe murchar.", Step_ESP = "Añade espinacas y deja que se marchiten." },
                new() { Step_DE = "Rühre einen Löffel Pesto in die fertige Pasta.", Step_EN = "Stir a spoonful of pesto into the finished pasta.", Step_PRT = "Misture uma colher de pesto na massa pronta.", Step_ESP = "Incorpora una cucharada de pesto a la pasta." },
                new() { Step_DE = "Bestreue die Pasta mit Zitronenabrieb.", Step_EN = "Sprinkle the pasta with lemon zest.", Step_PRT = "Polvilhe a massa com raspas de limão.", Step_ESP = "Espolvorea la pasta con ralladura de limón." },
                new() { Step_DE = "Press den Saft einer Zitrone über die Pasta.", Step_EN = "Squeeze lemon juice over the pasta.", Step_PRT = "Esprema suco de limão sobre a massa.", Step_ESP = "Exprime jugo de limón sobre la pasta." },
                new() { Step_DE = "Schneide Pilze in Scheiben für eine cremige Sauce.", Step_EN = "Slice mushrooms for a creamy sauce.", Step_PRT = "Fatie cogumelos para um molho cremoso.", Step_ESP = "Corta los champiñones en láminas para una salsa cremosa." },
                new() { Step_DE = "Brate die Pilze kräftig an, bis sie bräunen.", Step_EN = "Sear the mushrooms until browned.", Step_PRT = "Doureich os cogumelos até dourarem.", Step_ESP = "Dora los champiñones hasta que se doren." },
                new() { Step_DE = "Gieße etwas Brühe zu den Pilzen und lasse sie einkochen.", Step_EN = "Add a splash of broth to the mushrooms and reduce.", Step_PRT = "Adicione um pouco de caldo aos cogumelos e reduza.", Step_ESP = "Añade un poco de caldo a los champiñones y reduce." },
                new() { Step_DE = "Rühre Frischkäse ein, bis die Sauce cremig wird.", Step_EN = "Stir in cream cheese until the sauce is creamy.", Step_PRT = "Misture cream cheese até o molho ficar cremoso.", Step_ESP = "Incorpora queso crema hasta que la salsa esté cremosa." },
                new() { Step_DE = "Mische die Pilzsauce mit der Pasta.", Step_EN = "Combine the mushroom sauce with the pasta.", Step_PRT = "Misture o molho de cogumelos com a massa.", Step_ESP = "Mezcla la salsa de champiñones con la pasta." },
                new() { Step_DE = "Schneide getrocknete Tomaten in Streifen.", Step_EN = "Slice sun-dried tomatoes into strips.", Step_PRT = "Corte tomates secos em tiras.", Step_ESP = "Corta tomates secos en tiras." },
                new() { Step_DE = "Gib die getrockneten Tomaten kurz zur Sauce.", Step_EN = "Add the sun-dried tomatoes briefly to the sauce.", Step_PRT = "Adicione os tomates secos rapidamente ao molho.", Step_ESP = "Añade brevemente los tomates secos a la salsa." },
                new() { Step_DE = "Schneide die Aubergine in kleine Würfel für Pasta alla Norma.", Step_EN = "Dice the eggplant for pasta alla Norma.", Step_PRT = "Corte a berinjela em cubos para a massa alla Norma.", Step_ESP = "Corta la berenjena en cubos para pasta alla Norma." },
                new() { Step_DE = "Salze die Auberginen und lasse sie 10 Minuten ziehen.", Step_EN = "Salt the eggplant and let it sit for 10 minutes.", Step_PRT = "Salpique a berinjela e deixe descansar por 10 minutos.", Step_ESP = "Sala la berenjena y deja reposar 10 minutos." },
                new() { Step_DE = "Tupfe die Auberginen trocken und brate sie goldbraun.", Step_EN = "Pat the eggplant dry and fry until golden.", Step_PRT = "Seque a berinjela e frite até dourar.", Step_ESP = "Seca la berenjena y fríe hasta dorar." },
                new() { Step_DE = "Mische die Auberginen unter die Tomatensauce.", Step_EN = "Mix the eggplant into the tomato sauce.", Step_PRT = "Misture a berinjela ao molho de tomate.", Step_ESP = "Mezcla la berenjena en la salsa de tomate." },
                new() { Step_DE = "Schneide Brokkoli in kleine Röschen.", Step_EN = "Cut broccoli into small florets.", Step_PRT = "Corte o brócolis em floretes pequenos.", Step_ESP = "Corta el brócoli en floretes pequeños." },
                new() { Step_DE = "Blanchiere den Brokkoli 2 Minuten in Salzwasser.", Step_EN = "Blanch the broccoli in salted water for 2 minutes.", Step_PRT = "Branqueie o brócolis em água salgada por 2 minutos.", Step_ESP = "Escalda el brócoli en agua con sal durante 2 minutos." },
                new() { Step_DE = "Schwenke den Brokkoli kurz in der Pastasauce.", Step_EN = "Toss the broccoli briefly in the pasta sauce.", Step_PRT = "Salteie o brócolis rapidamente no molho.", Step_ESP = "Saltea el brócoli brevemente en la salsa." },
                new() { Step_DE = "Schneide Spargel in kurze Stücke für Pasta.", Step_EN = "Cut asparagus into short pieces for pasta.", Step_PRT = "Corte o aspargo em pedaços curtos para a massa.", Step_ESP = "Corta los espárragos en trozos cortos para la pasta." },
                new() { Step_DE = "Gare den Spargel in der Sauce, bis er zart ist.", Step_EN = "Cook the asparagus in the sauce until tender.", Step_PRT = "Cozinhe o aspargo no molho até ficar macio.", Step_ESP = "Cocina los espárragos en la salsa hasta que estén tiernos." },
                new() { Step_DE = "Schneide die Pasta mit etwas Zitronenschale an.", Step_EN = "Finish the pasta with a bit of lemon zest.", Step_PRT = "Finalize a massa com raspas de limão.", Step_ESP = "Termina la pasta con un poco de ralladura de limón." },
                new() { Step_DE = "Bestreue die Pasta mit frisch gemahlenem Pfeffer.", Step_EN = "Sprinkle the pasta with freshly ground pepper.", Step_PRT = "Polvilhe a massa com pimenta moída na hora.", Step_ESP = "Espolvorea la pasta con pimienta recién molida." },
                new() { Step_DE = "Schäle die Karotten für den Schmorbraten.", Step_EN = "Peel the carrots for the pot roast.", Step_PRT = "Descasque as cenouras para o assado.", Step_ESP = "Pela las zanahorias para el asado." },
                new() { Step_DE = "Schneide Karotten, Sellerie und Lauch in grobe Stücke.", Step_EN = "Cut carrots, celery, and leek into large pieces.", Step_PRT = "Corte cenouras, aipo e alho-poró em pedaços grandes.", Step_ESP = "Corta zanahorias, apio y puerro en trozos grandes." },
                new() { Step_DE = "Tupfe den Braten trocken und würze ihn kräftig.", Step_EN = "Pat the roast dry and season it generously.", Step_PRT = "Seque o assado e tempere bem.", Step_ESP = "Seca el asado y sazónalo generosamente." },
                new() { Step_DE = "Erhitze Öl in einem Bräter und brate den Braten rundum an.", Step_EN = "Heat oil in a Dutch oven and sear the roast on all sides.", Step_PRT = "Aqueça óleo numa panela pesada e sele o assado por todos os lados.", Step_ESP = "Calienta aceite en una cazuela y dora el asado por todos los lados." },
                new() { Step_DE = "Nimm den Braten heraus und lege ihn beiseite.", Step_EN = "Remove the roast and set it aside.", Step_PRT = "Retire o assado e reserve.", Step_ESP = "Retira el asado y resérvalo." },
                new() { Step_DE = "Brate das Wurzelgemüse im Bratensatz an.", Step_EN = "Sauté the root vegetables in the pan drippings.", Step_PRT = "Salteie os legumes na gordura da panela.", Step_ESP = "Saltea las verduras en los jugos de la cazuela." },
                new() { Step_DE = "Gib Tomatenmark hinzu und röste es kurz mit.", Step_EN = "Add tomato paste and toast it briefly.", Step_PRT = "Adicione extrato de tomate e toste rapidamente.", Step_ESP = "Añade concentrado de tomate y tuéstalo brevemente." },
                new() { Step_DE = "Lösche mit Rotwein ab und löse den Bratensatz.", Step_EN = "Deglaze with red wine and loosen the fond.", Step_PRT = "Deglace com vinho tinto e solte o fundo.", Step_ESP = "Desglasa con vino tinto y desprende los jugos." },
                new() { Step_DE = "Lass den Rotwein etwas einkochen.", Step_EN = "Let the red wine reduce.", Step_PRT = "Deixe o vinho tinto reduzir.", Step_ESP = "Deja reducir el vino tinto." },
                new() { Step_DE = "Gib Brühe und Lorbeerblätter in den Bräter.", Step_EN = "Add broth and bay leaves to the pot.", Step_PRT = "Adicione caldo e folhas de louro à panela.", Step_ESP = "Añade caldo y hojas de laurel a la cazuela." },
                new() { Step_DE = "Lege den Braten zurück in den Bräter.", Step_EN = "Return the roast to the pot.", Step_PRT = "Coloque o assado de volta na panela.", Step_ESP = "Devuelve el asado a la cazuela." },
                new() { Step_DE = "Decke den Bräter ab und schmore das Fleisch 2 bis 3 Stunden.", Step_EN = "Cover and braise the meat for 2 to 3 hours.", Step_PRT = "Tampe e braseie a carne por 2 a 3 horas.", Step_ESP = "Tapa y brasea la carne de 2 a 3 horas." },
                new() { Step_DE = "Wende den Braten gelegentlich während des Schmorens.", Step_EN = "Turn the roast occasionally while braising.", Step_PRT = "Vire o assado ocasionalmente durante o braseado.", Step_ESP = "Da la vuelta al asado de vez en cuando durante el estofado." },
                new() { Step_DE = "Prüfe den Gargrad, das Fleisch soll zart sein.", Step_EN = "Check doneness; the meat should be tender.", Step_PRT = "Verifique o ponto; a carne deve ficar macia.", Step_ESP = "Comprueba el punto; la carne debe estar tierna." },
                new() { Step_DE = "Nimm den Braten heraus und halte ihn warm.", Step_EN = "Remove the roast and keep it warm.", Step_PRT = "Retire o assado e mantenha-o aquecido.", Step_ESP = "Retira el asado y mantenlo caliente." },
                new() { Step_DE = "Passiere die Sauce durch ein Sieb.", Step_EN = "Strain the sauce through a sieve.", Step_PRT = "Coe o molho por uma peneira.", Step_ESP = "Cuela la salsa con un colador." },
                new() { Step_DE = "Reduziere die Sauce bei Bedarf für mehr Geschmack.", Step_EN = "Reduce the sauce if needed for more flavor.", Step_PRT = "Reduza o molho se necessário para intensificar o sabor.", Step_ESP = "Reduce la salsa si es necesario para más sabor." },
                new() { Step_DE = "Schmecke die Sauce mit Salz und Pfeffer ab.", Step_EN = "Season the sauce with salt and pepper.", Step_PRT = "Tempere o molho com sal e pimenta.", Step_ESP = "Sazona la salsa con sal y pimienta." },
                new() { Step_DE = "Rühre kalte Butter in die heiße Sauce ein.", Step_EN = "Whisk cold butter into the hot sauce.", Step_PRT = "Misture manteiga fria no molho quente.", Step_ESP = "Incorpora mantequilla fría a la salsa caliente." },
                new() { Step_DE = "Schneide den Braten in Scheiben zum Servieren.", Step_EN = "Slice the roast for serving.", Step_PRT = "Fatie o assado para servir.", Step_ESP = "Corta el asado en rebanadas para servir." },
                new() { Step_DE = "Serviere den Braten mit der Sauce überzogen.", Step_EN = "Serve the roast with sauce spooned over.", Step_PRT = "Sirva o assado com molho por cima.", Step_ESP = "Sirve el asado con salsa por encima." },
                new() { Step_DE = "Lege den Braten auf ein Bett aus Gemüse im Bräter.", Step_EN = "Place the roast on a bed of vegetables in the pot.", Step_PRT = "Coloque o assado sobre uma cama de legumes na panela.", Step_ESP = "Coloca el asado sobre una cama de verduras en la cazuela." },
                new() { Step_DE = "Gib frische Kräuterzweige in den Bräter.", Step_EN = "Add fresh herb sprigs to the pot.", Step_PRT = "Adicione ramos de ervas frescas à panela.", Step_ESP = "Añade ramas de hierbas frescas a la cazuela." },
                new() { Step_DE = "Lass das Fleisch vor dem Aufschneiden 10 Minuten ruhen.", Step_EN = "Let the meat rest for 10 minutes before slicing.", Step_PRT = "Deixe a carne descansar 10 minutos antes de fatiar.", Step_ESP = "Deja reposar la carne 10 minutos antes de cortar." },
                new() { Step_DE = "Schöpfe Fett von der Sauce ab, falls nötig.", Step_EN = "Skim fat from the sauce if needed.", Step_PRT = "Retire a gordura do molho se necessário.", Step_ESP = "Retira la grasa de la salsa si es necesario." },
                new() { Step_DE = "Gib eine Prise Zucker in die Sauce, um die Säure zu runden.", Step_EN = "Add a pinch of sugar to balance the sauce acidity.", Step_PRT = "Adicione uma pitada de açúcar para equilibrar a acidez.", Step_ESP = "Añade una pizca de azúcar para equilibrar la acidez." },
                new() { Step_DE = "Rühre einen Löffel Senf in die Sauce für mehr Tiefe.", Step_EN = "Stir a spoonful of mustard into the sauce for depth.", Step_PRT = "Misture uma colher de mostarda no molho para mais profundidade.", Step_ESP = "Incorpora una cucharada de mostaza a la salsa para más profundidad." },
                new() { Step_DE = "Lege den Deckel leicht schräg auf, damit die Sauce einkocht.", Step_EN = "Leave the lid slightly ajar so the sauce reduces.", Step_PRT = "Deixe a tampa levemente aberta para reduzir o molho.", Step_ESP = "Deja la tapa ligeramente entreabierta para reducir la salsa." },
                new() { Step_DE = "Schmore den Braten im Ofen bei 160 °C für gleichmäßige Hitze.", Step_EN = "Braise the roast in the oven at 160°C for even heat.", Step_PRT = "Braseie o assado no forno a 160 °C para calor uniforme.", Step_ESP = "Brasea el asado en el horno a 160 °C para calor uniforme." },
                new() { Step_DE = "Gieße zwischendurch etwas Flüssigkeit nach, falls nötig.", Step_EN = "Add a little liquid if needed during braising.", Step_PRT = "Adicione um pouco de líquido se necessário durante o cozimento.", Step_ESP = "Añade un poco de líquido si es necesario durante el estofado." },
                new() { Step_DE = "Lass das Gemüse in der Sauce vollständig weich werden.", Step_EN = "Let the vegetables become fully tender in the sauce.", Step_PRT = "Deixe os legumes ficarem bem macios no molho.", Step_ESP = "Deja que las verduras se ablanden por completo en la salsa." },
                new() { Step_DE = "Zerdrücke einen Teil des Gemüses, um die Sauce zu binden.", Step_EN = "Mash some of the vegetables to thicken the sauce.", Step_PRT = "Amasse parte dos legumes para engrossar o molho.", Step_ESP = "Aplasta parte de las verduras para espesar la salsa." },
                new() { Step_DE = "Bestreue den Braten vor dem Servieren mit Kräutern.", Step_EN = "Sprinkle the roast with herbs before serving.", Step_PRT = "Polvilhe o assado com ervas antes de servir.", Step_ESP = "Espolvorea el asado con hierbas antes de servir." },
                new() { Step_DE = "Serviere den Braten mit Kartoffelbeilage deiner Wahl.", Step_EN = "Serve the roast with your preferred potato side.", Step_PRT = "Sirva o assado com o acompanhamento de batata de sua escolha.", Step_ESP = "Sirve el asado con la guarnición de patata que prefieras." },
                new() { Step_DE = "Stelle den Braten kurz unter den Grill für mehr Farbe.", Step_EN = "Place the roast briefly under the broiler for more color.", Step_PRT = "Coloque o assado rapidamente sob o grill para mais cor.", Step_ESP = "Pasa el asado un momento bajo el grill para más color." },
                new() { Step_DE = "Vermenge die Bratensauce mit einem Schluck Sahne.", Step_EN = "Mix the roast sauce with a splash of cream.", Step_PRT = "Misture o molho do assado com um pouco de creme.", Step_ESP = "Mezcla la salsa del asado con un chorrito de nata." },
                new() { Step_DE = "Koche die Sauce kurz auf, nachdem du Sahne hinzugefügt hast.", Step_EN = "Bring the sauce to a brief boil after adding cream.", Step_PRT = "Ferva rapidamente o molho após adicionar o creme.", Step_ESP = "Hierve brevemente la salsa después de añadir nata." },
                new() { Step_DE = "Schneide die Reste in Würfel für ein späteres Ragout.", Step_EN = "Dice leftovers into cubes for a later ragout.", Step_PRT = "Corte as sobras em cubos para um ragu posterior.", Step_ESP = "Corta las sobras en cubos para un ragú posterior." },
                new() { Step_DE = "Erwärme die Reste langsam in der Sauce.", Step_EN = "Reheat leftovers gently in the sauce.", Step_PRT = "Aqueça as sobras lentamente no molho.", Step_ESP = "Recalienta las sobras suavemente en la salsa." },
                new() { Step_DE = "Schneide frische Petersilie für den Abschluss.", Step_EN = "Chop fresh parsley for the finish.", Step_PRT = "Pique salsa fresca para finalizar.", Step_ESP = "Pica perejil fresco para terminar." },
                new() { Step_DE = "Streue die Petersilie über Pasta oder Braten.", Step_EN = "Sprinkle the parsley over pasta or roast.", Step_PRT = "Polvilhe a salsa sobre a massa ou o assado.", Step_ESP = "Espolvorea el perejil sobre la pasta o el asado." },
                new() { Step_DE = "Koche die Pasta für 1 Minute kürzer, wenn sie im Ofen nachgart.", Step_EN = "Cook the pasta 1 minute less if it will finish in the oven.", Step_PRT = "Cozinhe a massa 1 minuto a menos se terminar no forno.", Step_ESP = "Cocina la pasta 1 minuto menos si terminará en el horno." },
                new() { Step_DE = "Mische die Pasta mit etwas Sauce, bevor du sie schichtest.", Step_EN = "Toss the pasta with a bit of sauce before layering.", Step_PRT = "Misture a massa com um pouco de molho antes de montar.", Step_ESP = "Mezcla la pasta con un poco de salsa antes de montar." },
                new() { Step_DE = "Backe die Pasta-Auflauf-Variante 20 Minuten.", Step_EN = "Bake the pasta casserole version for 20 minutes.", Step_PRT = "Asse a versão de massa ao forno por 20 minutos.", Step_ESP = "Hornea la versión de pasta al horno durante 20 minutos." },
                new() { Step_DE = "Lass den Pasta-Auflauf vor dem Servieren ruhen.", Step_EN = "Let the pasta bake rest before serving.", Step_PRT = "Deixe o gratinado descansar antes de servir.", Step_ESP = "Deja reposar la pasta al horno antes de servir." },
                new() { Step_DE = "Schmecke die Pastasauce mit Chili ab.", Step_EN = "Season the pasta sauce with chili.", Step_PRT = "Tempere o molho com pimenta.", Step_ESP = "Sazona la salsa con chile." },
                new() { Step_DE = "Zerstoße Pfefferkörner frisch für mehr Aroma.", Step_EN = "Crush peppercorns fresh for more aroma.", Step_PRT = "Esmague grãos de pimenta na hora para mais aroma.", Step_ESP = "Machaca granos de pimienta para más aroma." },
                new() { Step_DE = "Füge gerösteten Knoblauch zur Sauce hinzu.", Step_EN = "Add roasted garlic to the sauce.", Step_PRT = "Adicione alho assado ao molho.", Step_ESP = "Añade ajo asado a la salsa." },
                new() { Step_DE = "Rühre die Pasta mit einem Holzlöffel vorsichtig durch.", Step_EN = "Gently stir the pasta with a wooden spoon.", Step_PRT = "Misture a massa delicadamente com uma colher de madeira.", Step_ESP = "Remueve la pasta suavemente con una cuchara de madera." },
                new() { Step_DE = "Lege den Deckel auf die Pfanne, damit die Sauce eindickt.", Step_EN = "Cover the pan so the sauce thickens.", Step_PRT = "Tampe a frigideira para o molho engrossar.", Step_ESP = "Tapa la sartén para que la salsa espese." },
                new() { Step_DE = "Rolle den Braten beim Anbraten gleichmäßig, damit er Farbe bekommt.", Step_EN = "Rotate the roast while searing so it browns evenly.", Step_PRT = "Gire o assado ao selar para dourar por igual.", Step_ESP = "Gira el asado al dorarlo para que se dore uniformemente." },
                new() { Step_DE = "Zerdrücke Knoblauchzehen und gib sie in den Schmoransatz.", Step_EN = "Crush garlic cloves and add them to the braising base.", Step_PRT = "Esmague os dentes de alho e adicione à base do braseado.", Step_ESP = "Machaca los dientes de ajo y añádelos a la base del estofado." },
                new() { Step_DE = "Vermenge die Pasta mit gerösteten Semmelbröseln.", Step_EN = "Mix the pasta with toasted breadcrumbs.", Step_PRT = "Misture a massa com farinha de rosca tostada.", Step_ESP = "Mezcla la pasta con pan rallado tostado." },
                new() { Step_DE = "Röste Semmelbrösel in etwas Öl goldgelb.", Step_EN = "Toast breadcrumbs in a little oil until golden.", Step_PRT = "Torre a farinha de rosca em um pouco de óleo até dourar.", Step_ESP = "Tuesta el pan rallado en un poco de aceite hasta dorar." }
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
