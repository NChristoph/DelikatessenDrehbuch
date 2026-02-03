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
                new() { Step_DE = "Heize den Ofen auf 190 °C Ober-/Unterhitze vor.", Step_EN = "Preheat the oven to 190°C (top/bottom heat).", Step_PRT = "Pré-aqueça o forno a 190 °C (calor superior/inferior).", Step_ESP = "Precalienta el horno a 190 °C (calor arriba y abajo)." },
                new() { Step_DE = "Fette eine Auflaufform dünn ein.", Step_EN = "Grease a baking dish lightly.", Step_PRT = "Unte levemente uma assadeira.", Step_ESP = "Engrasa ligeramente una fuente para horno." },
                new() { Step_DE = "Bereite eine Tomatenbasis vor und schmecke sie mit Kräutern ab.", Step_EN = "Prepare a tomato base and season it with herbs.", Step_PRT = "Prepare uma base de tomate e tempere com ervas.", Step_ESP = "Prepara una base de tomate y sazónala con hierbas." },
                new() { Step_DE = "Hacke frische Kräuter fein und stelle sie bereit.", Step_EN = "Finely chop fresh herbs and set them aside.", Step_PRT = "Pique finamente ervas frescas e reserve.", Step_ESP = "Pica finamente hierbas frescas y reserva." },
                new() { Step_DE = "Schneide das Gemüse für die Füllung in kleine Würfel.", Step_EN = "Dice the vegetables for the filling into small cubes.", Step_PRT = "Corte os vegetais do recheio em cubos pequenos.", Step_ESP = "Corta las verduras del relleno en cubitos." },
                new() { Step_DE = "Schwitze das Gemüse in etwas Öl, bis es weich ist.", Step_EN = "Sauté the vegetables in a little oil until softened.", Step_PRT = "Salteie os vegetais em um pouco de óleo até amaciarem.", Step_ESP = "Sofríe las verduras con un poco de aceite hasta que estén tiernas." },
                new() { Step_DE = "Würze die Gemüsemischung mit Salz, Pfeffer und Oregano.", Step_EN = "Season the vegetable mixture with salt, pepper, and oregano.", Step_PRT = "Tempere a mistura de legumes com sal, pimenta e orégano.", Step_ESP = "Sazona la mezcla de verduras con sal, pimienta y orégano." },
                new() { Step_DE = "Lösche die Gemüsemischung mit passierten Tomaten ab.", Step_EN = "Deglaze the vegetable mixture with crushed tomatoes.", Step_PRT = "Deglace a mistura de legumes com tomate triturado.", Step_ESP = "Desglasa la mezcla de verduras con tomate triturado." },
                new() { Step_DE = "Lass die Tomatensauce 10 Minuten sanft köcheln.", Step_EN = "Let the tomato sauce simmer gently for 10 minutes.", Step_PRT = "Deixe o molho de tomate cozinhar suavemente por 10 minutos.", Step_ESP = "Deja que la salsa de tomate hierva a fuego lento durante 10 minutos." },
                new() { Step_DE = "Rühre die Sauce cremig glatt, falls sie zu grob ist.", Step_EN = "Blend the sauce smooth if it is too chunky.", Step_PRT = "Bata o molho até ficar liso se estiver muito grosso.", Step_ESP = "Tritura la salsa si está demasiado gruesa." },
                new() { Step_DE = "Verrühre Pflanzenmilch mit Stärke zu einer Béchamelbasis.", Step_EN = "Whisk plant milk with starch for a béchamel base.", Step_PRT = "Misture leite vegetal com amido para a base do béchamel.", Step_ESP = "Bate la leche vegetal con fécula para la base de la bechamel." },
                new() { Step_DE = "Gib die Béchamelbasis in den Topf und erhitze sie unter Rühren.", Step_EN = "Transfer the béchamel base to a pot and heat while stirring.", Step_PRT = "Passe a base do béchamel para a panela e aqueça mexendo.", Step_ESP = "Pasa la base de bechamel a la olla y caliéntala removiendo." },
                new() { Step_DE = "Schmecke die Béchamel mit Muskat und Salz ab.", Step_EN = "Season the béchamel with nutmeg and salt.", Step_PRT = "Tempere o béchamel com noz-moscada e sal.", Step_ESP = "Sazona la bechamel con nuez moscada y sal." },
                new() { Step_DE = "Füge nach Wunsch Hefeflocken für extra Würze hinzu.", Step_EN = "Add nutritional yeast for extra savoriness if desired.", Step_PRT = "Adicione levedura nutricional para mais sabor, se desejar.", Step_ESP = "Añade levadura nutricional para más sabor si lo deseas." },
                new() { Step_DE = "Koche Lasagneblätter in Salzwasser kurz vor.", Step_EN = "Parboil the lasagna sheets briefly in salted water.", Step_PRT = "Pré-cozinhe as folhas de lasanha em água salgada por pouco tempo.", Step_ESP = "Precuece las láminas de lasaña brevemente en agua con sal." },
                new() { Step_DE = "Lege die Lasagneblätter trocken auf ein Tuch.", Step_EN = "Lay the lasagna sheets on a towel to dry.", Step_PRT = "Coloque as folhas de lasanha sobre um pano para secar.", Step_ESP = "Coloca las láminas de lasaña sobre un paño para secarlas." },
                new() { Step_DE = "Schneide die Lasagneblätter passend für die Form zurecht.", Step_EN = "Trim the lasagna sheets to fit the dish.", Step_PRT = "Ajuste as folhas de lasanha ao tamanho da forma.", Step_ESP = "Recorta las láminas de lasaña para que encajen en la fuente." },
                new() { Step_DE = "Gib eine dünne Schicht Tomatensauce in die Auflaufform.", Step_EN = "Spread a thin layer of tomato sauce in the baking dish.", Step_PRT = "Espalhe uma camada fina de molho de tomate na assadeira.", Step_ESP = "Extiende una capa fina de salsa de tomate en la fuente." },
                new() { Step_DE = "Lege die erste Schicht Lasagneblätter ein.", Step_EN = "Lay down the first layer of lasagna sheets.", Step_PRT = "Coloque a primeira camada de folhas de lasanha.", Step_ESP = "Coloca la primera capa de láminas de lasaña." },
                new() { Step_DE = "Verteile eine Schicht Gemüsefüllung auf den Blättern.", Step_EN = "Spread a layer of vegetable filling over the sheets.", Step_PRT = "Espalhe uma camada de recheio de legumes sobre as folhas.", Step_ESP = "Distribuye una capa de relleno de verduras sobre las láminas." },
                new() { Step_DE = "Gieße etwas Béchamel gleichmäßig darüber.", Step_EN = "Pour some béchamel evenly over the top.", Step_PRT = "Regue com um pouco de béchamel de forma uniforme.", Step_ESP = "Vierte un poco de bechamel de manera uniforme." },
                new() { Step_DE = "Wiederhole die Schichten, bis alle Zutaten aufgebraucht sind.", Step_EN = "Repeat the layers until all ingredients are used.", Step_PRT = "Repita as camadas até terminar os ingredientes.", Step_ESP = "Repite las capas hasta que se terminen los ingredientes." },
                new() { Step_DE = "Beende die Lasagne mit einer Schicht Béchamel.", Step_EN = "Finish the lasagna with a layer of béchamel.", Step_PRT = "Finalize a lasanha com uma camada de béchamel.", Step_ESP = "Termina la lasaña con una capa de bechamel." },
                new() { Step_DE = "Streue geriebenen Käse oder vegane Alternative darüber.", Step_EN = "Sprinkle grated cheese or a vegan alternative on top.", Step_PRT = "Polvilhe queijo ralado ou alternativa vegana por cima.", Step_ESP = "Espolvorea queso rallado o una alternativa vegana encima." },
                new() { Step_DE = "Decke die Form locker mit Folie ab.", Step_EN = "Loosely cover the dish with foil.", Step_PRT = "Cubra a assadeira frouxamente com papel alumínio.", Step_ESP = "Cubre la fuente con papel aluminio sin apretar." },
                new() { Step_DE = "Backe die Lasagne 25 Minuten abgedeckt.", Step_EN = "Bake the lasagna covered for 25 minutes.", Step_PRT = "Asse a lasanha coberta por 25 minutos.", Step_ESP = "Hornea la lasaña tapada durante 25 minutos." },
                new() { Step_DE = "Entferne die Folie und backe weitere 10 Minuten.", Step_EN = "Remove the foil and bake for another 10 minutes.", Step_PRT = "Retire o papel alumínio e asse por mais 10 minutos.", Step_ESP = "Retira el papel aluminio y hornea 10 minutos más." },
                new() { Step_DE = "Lass die Lasagne vor dem Anschneiden 10 Minuten ruhen.", Step_EN = "Let the lasagna rest for 10 minutes before slicing.", Step_PRT = "Deixe a lasanha descansar 10 minutos antes de cortar.", Step_ESP = "Deja reposar la lasaña 10 minutos antes de cortarla." },
                new() { Step_DE = "Schneide die Portionen sauber mit einem scharfen Messer.", Step_EN = "Cut clean portions with a sharp knife.", Step_PRT = "Corte porções limpas com uma faca afiada.", Step_ESP = "Corta porciones limpias con un cuchillo afilado." },
                new() { Step_DE = "Garniere die Lasagne mit frischen Kräutern.", Step_EN = "Garnish the lasagna with fresh herbs.", Step_PRT = "Decore a lasanha com ervas frescas.", Step_ESP = "Decora la lasaña con hierbas frescas." },
                new() { Step_DE = "Serviere die Lasagne mit einem knackigen Salat.", Step_EN = "Serve the lasagna with a crisp salad.", Step_PRT = "Sirva a lasanha com uma salada crocante.", Step_ESP = "Sirve la lasaña con una ensalada crujiente." },
                new() { Step_DE = "Bereite eine vegane Ricotta-Alternative aus Tofu vor.", Step_EN = "Prepare a vegan ricotta alternative from tofu.", Step_PRT = "Prepare uma alternativa vegana de ricota com tofu.", Step_ESP = "Prepara una alternativa vegana a la ricotta con tofu." },
                new() { Step_DE = "Vermenge den Tofu mit Zitronensaft und Kräutern.", Step_EN = "Mix the tofu with lemon juice and herbs.", Step_PRT = "Misture o tofu com suco de limão e ervas.", Step_ESP = "Mezcla el tofu con jugo de limón y hierbas." },
                new() { Step_DE = "Trage die Tofumasse in dünnen Klecksen auf die Schichten auf.", Step_EN = "Dot the tofu mixture onto the layers.", Step_PRT = "Distribua a mistura de tofu em pequenas porções nas camadas.", Step_ESP = "Reparte la mezcla de tofu en pequeños puntos sobre las capas." },
                new() { Step_DE = "Röste Pinienkerne kurz in einer Pfanne und stelle sie bereit.", Step_EN = "Toast pine nuts briefly in a pan and set aside.", Step_PRT = "Torre rapidamente pinhões numa frigideira e reserve.", Step_ESP = "Tuesta piñones brevemente en una sartén y reserva." },
                new() { Step_DE = "Streue die gerösteten Kerne kurz vor dem Servieren darüber.", Step_EN = "Sprinkle the toasted nuts over the dish just before serving.", Step_PRT = "Polvilhe os pinhões torrados pouco antes de servir.", Step_ESP = "Espolvorea los piñones tostados justo antes de servir." },
                new() { Step_DE = "Bereite ein schnelles Pesto für die Lasagne vor.", Step_EN = "Prepare a quick pesto for the lasagna.", Step_PRT = "Prepare um pesto rápido para a lasanha.", Step_ESP = "Prepara un pesto rápido para la lasaña." },
                new() { Step_DE = "Gib einen Löffel Pesto zwischen die Schichten.", Step_EN = "Add a spoonful of pesto between the layers.", Step_PRT = "Adicione uma colher de pesto entre as camadas.", Step_ESP = "Añade una cucharada de pesto entre las capas." },
                new() { Step_DE = "Schmecke die Lasagne-Schichten vor dem Backen noch einmal ab.", Step_EN = "Taste the layers once more before baking.", Step_PRT = "Prove as camadas novamente antes de assar.", Step_ESP = "Prueba las capas una vez más antes de hornear." },
                new() { Step_DE = "Erhitze Gemüsebrühe für eine leichte Suppe.", Step_EN = "Heat vegetable broth for a light soup.", Step_PRT = "Aqueça o caldo de legumes para uma sopa leve.", Step_ESP = "Calienta caldo de verduras para una sopa ligera." },
                new() { Step_DE = "Gib kleingeschnittenes Gemüse in die Brühe.", Step_EN = "Add diced vegetables to the broth.", Step_PRT = "Adicione legumes picados ao caldo.", Step_ESP = "Añade verduras picadas al caldo." },
                new() { Step_DE = "Lass das Gemüse in der Brühe weich garen.", Step_EN = "Cook the vegetables in the broth until tender.", Step_PRT = "Cozinhe os legumes no caldo até ficarem macios.", Step_ESP = "Cocina las verduras en el caldo hasta que estén tiernas." },
                new() { Step_DE = "Püriere die Suppe cremig und glatt.", Step_EN = "Blend the soup until creamy and smooth.", Step_PRT = "Bata a sopa até ficar cremosa e lisa.", Step_ESP = "Tritura la sopa hasta que quede cremosa y suave." },
                new() { Step_DE = "Verdünne die Suppe bei Bedarf mit etwas Brühe.", Step_EN = "Thin the soup with a little broth if needed.", Step_PRT = "Dilua a sopa com um pouco de caldo se necessário.", Step_ESP = "Aclara la sopa con un poco de caldo si es necesario." },
                new() { Step_DE = "Schmecke die Suppe mit Salz, Pfeffer und Kräutern ab.", Step_EN = "Season the soup with salt, pepper, and herbs.", Step_PRT = "Tempere a sopa com sal, pimenta e ervas.", Step_ESP = "Sazona la sopa con sal, pimienta y hierbas." },
                new() { Step_DE = "Rühre pflanzliche Sahne in die Suppe für mehr Cremigkeit.", Step_EN = "Stir plant-based cream into the soup for extra creaminess.", Step_PRT = "Misture creme vegetal na sopa para mais cremosidade.", Step_ESP = "Incorpora crema vegetal a la sopa para más cremosidad." },
                new() { Step_DE = "Gib kurz vor dem Servieren frischen Spinat in die Suppe.", Step_EN = "Add fresh spinach to the soup just before serving.", Step_PRT = "Adicione espinafre fresco à sopa pouco antes de servir.", Step_ESP = "Añade espinacas frescas a la sopa justo antes de servir." },
                new() { Step_DE = "Schmecke die Suppe mit einem Spritzer Zitronensaft ab.", Step_EN = "Finish the soup with a splash of lemon juice.", Step_PRT = "Finalize a sopa com um toque de suco de limão.", Step_ESP = "Termina la sopa con un chorrito de jugo de limón." },
                new() { Step_DE = "Serviere die Suppe mit geröstetem Brot.", Step_EN = "Serve the soup with toasted bread.", Step_PRT = "Sirva a sopa com pão torrado.", Step_ESP = "Sirve la sopa con pan tostado." },
                new() { Step_DE = "Garniere die Suppe mit frischen Kräutern.", Step_EN = "Garnish the soup with fresh herbs.", Step_PRT = "Decore a sopa com ervas frescas.", Step_ESP = "Decora la sopa con hierbas frescas." }
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
