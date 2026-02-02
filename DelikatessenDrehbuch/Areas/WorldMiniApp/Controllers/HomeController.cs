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
                new() { Step_DE = "Spüle den Reis gründlich mit kaltem Wasser, bis das Wasser klar bleibt.", Step_EN = "Rinse the rice with cold water until the water runs clear.", Step_PRT = "Lave o arroz com água fria até a água ficar transparente.", Step_ESP = "Enjuaga el arroz con agua fría hasta que el agua salga clara." },
                new() { Step_DE = "Gib den Reis mit der doppelten Menge Wasser in einen Topf und bringe ihn zum Kochen.", Step_EN = "Add the rice with twice the amount of water to a pot and bring to a boil.", Step_PRT = "Coloque o arroz com o dobro de água numa panela e leve ao fogo até ferver.", Step_ESP = "Pon el arroz con el doble de agua en una olla y llévalo a ebullición." },
                new() { Step_DE = "Reduziere die Hitze, decke den Topf ab und lasse den Reis 12 Minuten ziehen.", Step_EN = "Reduce the heat, cover the pot, and let the rice steam for 12 minutes.", Step_PRT = "Reduza o fogo, tampe a panela e deixe o arroz cozinhar por 12 minutos.", Step_ESP = "Baja el fuego, tapa la olla y deja que el arroz se cocine al vapor durante 12 minutos." },
                new() { Step_DE = "Wasche den Salat gründlich und schleudere ihn trocken.", Step_EN = "Wash the lettuce thoroughly and spin it dry.", Step_PRT = "Lave bem a alface e seque-a na centrifugadora.", Step_ESP = "Lava bien la lechuga y escúrrela en una centrifugadora." },
                new() { Step_DE = "Schneide die Tomaten in mundgerechte Stücke.", Step_EN = "Cut the tomatoes into bite-sized pieces.", Step_PRT = "Corte os tomates em pedaços de tamanho para comer.", Step_ESP = "Corta los tomates en trozos del tamaño de un bocado." },
                new() { Step_DE = "Schneide die Gurke in dünne Scheiben.", Step_EN = "Slice the cucumber into thin rounds.", Step_PRT = "Corte o pepino em rodelas finas.", Step_ESP = "Corta el pepino en rodajas finas." },
                new() { Step_DE = "Schneide die Paprika in feine Streifen.", Step_EN = "Cut the bell pepper into thin strips.", Step_PRT = "Corte o pimentão em tiras finas.", Step_ESP = "Corta el pimiento en tiras finas." },
                new() { Step_DE = "Schäle den Apfel, entferne das Kerngehäuse und würfle ihn.", Step_EN = "Peel the apple, remove the core, and dice it.", Step_PRT = "Descasque a maçã, retire o miolo e corte em cubos.", Step_ESP = "Pela la manzana, retira el corazón y córtala en cubos." },
                new() { Step_DE = "Reibe die Zitronenschale fein ab.", Step_EN = "Finely zest the lemon peel.", Step_PRT = "Rale finamente a casca do limão.", Step_ESP = "Ralla finamente la cáscara de limón." },
                new() { Step_DE = "Presse den Zitronensaft aus.", Step_EN = "Squeeze the lemon juice.", Step_PRT = "Esprema o suco do limão.", Step_ESP = "Exprime el jugo de limón." },
                new() { Step_DE = "Verrühre Öl, Essig, Salz und Pfeffer zu einem Dressing.", Step_EN = "Whisk oil, vinegar, salt, and pepper into a dressing.", Step_PRT = "Bata o óleo, o vinagre, o sal e a pimenta para fazer um molho.", Step_ESP = "Bate el aceite, el vinagre, la sal y la pimienta para hacer un aderezo." },
                new() { Step_DE = "Mische den Salat mit dem Dressing und richte ihn an.", Step_EN = "Toss the salad with the dressing and serve.", Step_PRT = "Misture a salada com o molho e sirva.", Step_ESP = "Mezcla la ensalada con el aderezo y sirve." },
                new() { Step_DE = "Schäle die Garnelen und entferne den Darm.", Step_EN = "Peel the shrimp and remove the vein.", Step_PRT = "Descasque os camarões e retire o intestino.", Step_ESP = "Pela los camarones y retira la vena." },
                new() { Step_DE = "Erhitze Öl in einer Pfanne und brate die Garnelen 2–3 Minuten.", Step_EN = "Heat oil in a pan and sauté the shrimp for 2–3 minutes.", Step_PRT = "Aqueça o óleo numa frigideira e salteie os camarões por 2–3 minutos.", Step_ESP = "Calienta aceite en una sartén y saltea los camarones 2–3 minutos." },
                new() { Step_DE = "Gib einen Schuss Weißwein hinzu und lass ihn kurz einkochen.", Step_EN = "Add a splash of white wine and let it reduce briefly.", Step_PRT = "Adicione um pouco de vinho branco e deixe reduzir rapidamente.", Step_ESP = "Añade un chorrito de vino blanco y deja reducir brevemente." },
                new() { Step_DE = "Rühre die Sahne ein und lasse die Sauce cremig werden.", Step_EN = "Stir in the cream and let the sauce turn creamy.", Step_PRT = "Junte o creme e deixe o molho ficar cremoso.", Step_ESP = "Incorpora la nata y deja que la salsa quede cremosa." },
                new() { Step_DE = "Schneide die Champignons in Scheiben.", Step_EN = "Slice the mushrooms.", Step_PRT = "Fatie os cogumelos.", Step_ESP = "Corta los champiñones en láminas." },
                new() { Step_DE = "Brate die Champignons an, bis sie goldbraun sind.", Step_EN = "Sauté the mushrooms until golden brown.", Step_PRT = "Salteie os cogumelos até dourarem.", Step_ESP = "Saltea los champiñones hasta que estén dorados." },
                new() { Step_DE = "Hacke die Petersilie fein.", Step_EN = "Finely chop the parsley.", Step_PRT = "Pique a salsa finamente.", Step_ESP = "Pica finamente el perejil." },
                new() { Step_DE = "Bestreue das Gericht mit frischer Petersilie.", Step_EN = "Sprinkle the dish with fresh parsley.", Step_PRT = "Polvilhe o prato com salsa fresca.", Step_ESP = "Espolvorea el plato con perejil fresco." },
                new() { Step_DE = "Schäle die Kartoffeln und reibe sie grob.", Step_EN = "Peel the potatoes and grate them coarsely.", Step_PRT = "Descasque as batatas e rale-as grosseiramente.", Step_ESP = "Pela las patatas y rállalas grueso." },
                new() { Step_DE = "Drücke die geriebenen Kartoffeln in einem Tuch aus.", Step_EN = "Squeeze the grated potatoes in a cloth.", Step_PRT = "Esprema as batatas raladas num pano.", Step_ESP = "Exprime las patatas ralladas en un paño." },
                new() { Step_DE = "Vermenge die Kartoffeln mit Ei, Salz und Pfeffer.", Step_EN = "Mix the potatoes with egg, salt, and pepper.", Step_PRT = "Misture as batatas com ovo, sal e pimenta.", Step_ESP = "Mezcla las patatas con huevo, sal y pimienta." },
                new() { Step_DE = "Forme kleine Puffer und brate sie in Öl knusprig.", Step_EN = "Form small patties and fry them in oil until crisp.", Step_PRT = "Forme pequenos bolinhos e frite-os no óleo até ficarem crocantes.", Step_ESP = "Forma pequeñas tortitas y fríelas en aceite hasta que estén crujientes." },
                new() { Step_DE = "Schneide den Fisch in gleichmäßige Stücke.", Step_EN = "Cut the fish into even pieces.", Step_PRT = "Corte o peixe em pedaços uniformes.", Step_ESP = "Corta el pescado en trozos uniformes." },
                new() { Step_DE = "Würze den Fisch mit Salz, Pfeffer und Zitronensaft.", Step_EN = "Season the fish with salt, pepper, and lemon juice.", Step_PRT = "Tempere o peixe com sal, pimenta e suco de limão.", Step_ESP = "Sazona el pescado con sal, pimienta y jugo de limón." },
                new() { Step_DE = "Erhitze Butter in einer Pfanne und brate den Fisch auf jeder Seite 2–3 Minuten.", Step_EN = "Heat butter in a pan and fry the fish 2–3 minutes per side.", Step_PRT = "Aqueça a manteiga numa frigideira e frite o peixe por 2–3 minutos de cada lado.", Step_ESP = "Calienta mantequilla en una sartén y fríe el pescado 2–3 minutos por lado." },
                new() { Step_DE = "Halbiere die Avocado, entferne den Kern und würfle das Fruchtfleisch.", Step_EN = "Halve the avocado, remove the pit, and dice the flesh.", Step_PRT = "Corte o abacate ao meio, retire o caroço e corte a polpa em cubos.", Step_ESP = "Parte el aguacate, quita el hueso y corta la pulpa en cubos." },
                new() { Step_DE = "Zerdrücke die Avocado mit einer Gabel und rühre Limettensaft ein.", Step_EN = "Mash the avocado with a fork and stir in lime juice.", Step_PRT = "Amasse o abacate com um garfo e misture o suco de lima.", Step_ESP = "Machaca el aguacate con un tenedor y agrega jugo de lima." },
                new() { Step_DE = "Schneide die Zwiebel in feine Streifen.", Step_EN = "Cut the onion into thin strips.", Step_PRT = "Corte a cebola em tiras finas.", Step_ESP = "Corta la cebolla en tiras finas." },
                new() { Step_DE = "Mariniere die Zwiebel mit etwas Salz und Essig für 10 Minuten.", Step_EN = "Marinate the onion with a bit of salt and vinegar for 10 minutes.", Step_PRT = "Marine a cebola com um pouco de sal e vinagre por 10 minutos.", Step_ESP = "Marina la cebolla con un poco de sal y vinagre durante 10 minutos." },
                new() { Step_DE = "Hacke die frischen Kräuter und mische sie unter.", Step_EN = "Chop the fresh herbs and mix them in.", Step_PRT = "Pique as ervas frescas e misture.", Step_ESP = "Pica las hierbas frescas y mézclalas." },
                new() { Step_DE = "Schneide das Brot in Scheiben und röste es leicht.", Step_EN = "Slice the bread and lightly toast it.", Step_PRT = "Corte o pão em fatias e toste levemente.", Step_ESP = "Corta el pan en rebanadas y tuéstalo ligeramente." },
                new() { Step_DE = "Reibe die Brotscheiben mit einer halbierten Knoblauchzehe ein.", Step_EN = "Rub the bread slices with a halved garlic clove.", Step_PRT = "Esfregue as fatias de pão com um dente de alho cortado ao meio.", Step_ESP = "Frota las rebanadas con un diente de ajo partido." },
                new() { Step_DE = "Schneide den Käse in kleine Würfel.", Step_EN = "Cut the cheese into small cubes.", Step_PRT = "Corte o queijo em cubos pequenos.", Step_ESP = "Corta el queso en cubitos." },
                new() { Step_DE = "Rühre Senf, Honig und Öl zu einer Marinade.", Step_EN = "Stir together mustard, honey, and oil into a marinade.", Step_PRT = "Misture mostarda, mel e óleo para fazer uma marinada.", Step_ESP = "Mezcla mostaza, miel y aceite para hacer una marinada." },
                new() { Step_DE = "Wende das Fleisch in der Marinade und lasse es 20 Minuten ziehen.", Step_EN = "Coat the meat in the marinade and let it rest for 20 minutes.", Step_PRT = "Passe a carne na marinada e deixe descansar por 20 minutos.", Step_ESP = "Cubre la carne con la marinada y deja reposar 20 minutos." },
                new() { Step_DE = "Erhitze eine Grillpfanne und grille das Fleisch von beiden Seiten.", Step_EN = "Heat a grill pan and grill the meat on both sides.", Step_PRT = "Aqueça uma frigideira grill e grelhe a carne dos dois lados.", Step_ESP = "Calienta una plancha y asa la carne por ambos lados." },
                new() { Step_DE = "Schneide die Zucchini in dünne Scheiben.", Step_EN = "Slice the zucchini thinly.", Step_PRT = "Fatie a abobrinha finamente.", Step_ESP = "Corta el calabacín en láminas finas." },
                new() { Step_DE = "Bestreiche die Zucchini mit Öl und grilliere sie kurz.", Step_EN = "Brush the zucchini with oil and grill briefly.", Step_PRT = "Pincele a abobrinha com óleo e grelhe rapidamente.", Step_ESP = "Unta el calabacín con aceite y ásalo brevemente." },
                new() { Step_DE = "Koche die Nudeln in kochendem Salzwasser al dente.", Step_EN = "Cook the pasta in salted boiling water until al dente.", Step_PRT = "Cozinhe a massa em água salgada até ficar al dente.", Step_ESP = "Cuece la pasta en agua con sal hasta que quede al dente." },
                new() { Step_DE = "Vermische die Nudeln mit der Sauce und gib etwas Nudelwasser dazu.", Step_EN = "Toss the pasta with the sauce and add a bit of pasta water.", Step_PRT = "Misture a massa com o molho e adicione um pouco da água do cozimento.", Step_ESP = "Mezcla la pasta con la salsa y añade un poco del agua de cocción." },
                new() { Step_DE = "Schmecke das Gericht mit Salz, Pfeffer und Kräutern ab.", Step_EN = "Season the dish with salt, pepper, and herbs.", Step_PRT = "Tempere o prato com sal, pimenta e ervas.", Step_ESP = "Sazona el plato con sal, pimienta y hierbas." },
                new() { Step_DE = "Schichte Joghurt, Obst und Müsli in Gläser.", Step_EN = "Layer yogurt, fruit, and granola in glasses.", Step_PRT = "Monte camadas de iogurte, fruta e granola em copos.", Step_ESP = "Coloca capas de yogur, fruta y granola en vasos." },
                new() { Step_DE = "Schmelze die Schokolade im Wasserbad.", Step_EN = "Melt the chocolate over a water bath.", Step_PRT = "Derreta o chocolate em banho-maria.", Step_ESP = "Derrite el chocolate al baño maría." },
                new() { Step_DE = "Rühre die geschmolzene Schokolade unter den Teig.", Step_EN = "Fold the melted chocolate into the batter.", Step_PRT = "Incorpore o chocolate derretido à massa.", Step_ESP = "Incorpora el chocolate derretido a la masa." },
                new() { Step_DE = "Fülle den Teig in eine gefettete Form.", Step_EN = "Pour the batter into a greased pan.", Step_PRT = "Despeje a massa numa forma untada.", Step_ESP = "Vierte la masa en un molde engrasado." },
                new() { Step_DE = "Backe den Kuchen im vorgeheizten Ofen 30–35 Minuten.", Step_EN = "Bake the cake in the preheated oven for 30–35 minutes.", Step_PRT = "Asse o bolo no forno pré-aquecido por 30–35 minutos.", Step_ESP = "Hornea el pastel en el horno precalentado durante 30–35 minutos." },
                new() { Step_DE = "Lasse den Kuchen auf einem Gitter vollständig auskühlen.", Step_EN = "Let the cake cool completely on a rack.", Step_PRT = "Deixe o bolo esfriar completamente numa grade.", Step_ESP = "Deja que el pastel se enfríe completamente sobre una rejilla." },
                new() { Step_DE = "Serviere das Gericht heiß und garniere es nach Wunsch.", Step_EN = "Serve the dish hot and garnish as desired.", Step_PRT = "Sirva o prato quente e decore a gosto.", Step_ESP = "Sirve el plato caliente y decora a gusto." }
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
