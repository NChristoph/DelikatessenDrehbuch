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
                new() { Step_DE = "Wasche den Blumenkohl und teile ihn in kleine Röschen.", Step_EN = "Wash the cauliflower and cut it into small florets.", Step_PRT = "Lave a couve-flor e separe em floretes pequenos.", Step_ESP = "Lava la coliflor y sepárala en ramilletes pequeños." },
                new() { Step_DE = "Wasche den Brokkoli und schneide die Stiele in Scheiben.", Step_EN = "Wash the broccoli and slice the stems.", Step_PRT = "Lave o brócolis e fatie os talos.", Step_ESP = "Lava el brócoli y corta los tallos en rodajas." },
                new() { Step_DE = "Schäle die Süßkartoffel und würfle sie fein.", Step_EN = "Peel the sweet potato and dice it finely.", Step_PRT = "Descasque a batata-doce e corte em cubos pequenos.", Step_ESP = "Pela la batata y córtala en cubitos." },
                new() { Step_DE = "Schäle die Rote Bete und schneide sie in Stifte.", Step_EN = "Peel the beetroot and cut it into sticks.", Step_PRT = "Descasque a beterraba e corte em palitos.", Step_ESP = "Pela la remolacha y córtala en bastones." },
                new() { Step_DE = "Schneide den Kürbis in mundgerechte Stücke.", Step_EN = "Cut the pumpkin into bite-sized pieces.", Step_PRT = "Corte a abóbora em pedaços para comer.", Step_ESP = "Corta la calabaza en trozos para bocado." },
                new() { Step_DE = "Schneide den Fenchel in dünne Streifen.", Step_EN = "Slice the fennel into thin strips.", Step_PRT = "Fatie o funcho em tiras finas.", Step_ESP = "Corta el hinojo en tiras finas." },
                new() { Step_DE = "Halbiere die Champignons und stelle sie bereit.", Step_EN = "Halve the mushrooms and set them aside.", Step_PRT = "Corte os cogumelos ao meio e reserve.", Step_ESP = "Parte los champiñones por la mitad y reserva." },
                new() { Step_DE = "Schäle die Schalotten und hacke sie fein.", Step_EN = "Peel the shallots and finely chop them.", Step_PRT = "Descasque as chalotas e pique finamente.", Step_ESP = "Pela las chalotas y pícalas finamente." },
                new() { Step_DE = "Schäle die Knoblauchzehen und schneide sie in Scheiben.", Step_EN = "Peel the garlic cloves and slice them.", Step_PRT = "Descasque os dentes de alho e fatie.", Step_ESP = "Pela los dientes de ajo y córtalos en láminas." },
                new() { Step_DE = "Schneide die Frühlingszwiebeln in Ringe.", Step_EN = "Slice the spring onions into rings.", Step_PRT = "Corte a cebolinha em rodelas.", Step_ESP = "Corta la cebolleta en aros." },
                new() { Step_DE = "Wasche die Kräuter und tupfe sie trocken.", Step_EN = "Wash the herbs and pat them dry.", Step_PRT = "Lave as ervas e seque com papel.", Step_ESP = "Lava las hierbas y sécalas." },
                new() { Step_DE = "Zupfe die Kräuterblätter von den Stielen.", Step_EN = "Pluck the herb leaves from the stems.", Step_PRT = "Retire as folhas das ervas dos talos.", Step_ESP = "Quita las hojas de las hierbas de los tallos." },
                new() { Step_DE = "Schneide die Kräuter grob für das Finish.", Step_EN = "Roughly chop the herbs for finishing.", Step_PRT = "Pique grosso as ervas para finalizar.", Step_ESP = "Pica las hierbas en trozos grandes para terminar." },
                new() { Step_DE = "Schneide die Zitronen in Spalten zum Servieren.", Step_EN = "Cut the lemons into wedges for serving.", Step_PRT = "Corte os limões em gomos para servir.", Step_ESP = "Corta los limones en gajos para servir." },
                new() { Step_DE = "Reibe die Orangenschale fein ab.", Step_EN = "Finely zest the orange peel.", Step_PRT = "Rale finamente a casca da laranja.", Step_ESP = "Ralla finamente la cáscara de la naranja." },
                new() { Step_DE = "Presse den Orangensaft aus.", Step_EN = "Squeeze the orange juice.", Step_PRT = "Esprema o suco de laranja.", Step_ESP = "Exprime el jugo de naranja." },
                new() { Step_DE = "Schneide den Apfel in dünne Scheiben.", Step_EN = "Slice the apple thinly.", Step_PRT = "Fatie a maçã finamente.", Step_ESP = "Corta la manzana en láminas finas." },
                new() { Step_DE = "Schneide die Birne in Würfel.", Step_EN = "Dice the pear.", Step_PRT = "Corte a pera em cubos.", Step_ESP = "Corta la pera en cubitos." },
                new() { Step_DE = "Wasche die Beeren vorsichtig und lasse sie abtropfen.", Step_EN = "Rinse the berries gently and let them drain.", Step_PRT = "Lave as bagas delicadamente e deixe escorrer.", Step_ESP = "Enjuaga las bayas con cuidado y deja escurrir." },
                new() { Step_DE = "Schneide die Paprika in Würfel.", Step_EN = "Dice the bell peppers.", Step_PRT = "Corte os pimentões em cubos.", Step_ESP = "Corta los pimientos en cubos." },
                new() { Step_DE = "Schneide die Gurke in feine Halbmonde.", Step_EN = "Slice the cucumber into thin half-moons.", Step_PRT = "Fatie o pepino em meias-luas finas.", Step_ESP = "Corta el pepino en medias lunas finas." },
                new() { Step_DE = "Schneide die Zucchini in lange Streifen.", Step_EN = "Cut the zucchini into long strips.", Step_PRT = "Corte a abobrinha em tiras longas.", Step_ESP = "Corta el calabacín en tiras largas." },
                new() { Step_DE = "Schneide die Aubergine in Scheiben.", Step_EN = "Slice the eggplant.", Step_PRT = "Fatie a berinjela.", Step_ESP = "Corta la berenjena en rodajas." },
                new() { Step_DE = "Salze die Auberginenscheiben und lasse sie schwitzen.", Step_EN = "Salt the eggplant slices and let them sweat.", Step_PRT = "Salpique as fatias de berinjela e deixe suar.", Step_ESP = "Sala las rodajas de berenjena y déjalas sudar." },
                new() { Step_DE = "Spüle die Auberginen kurz ab und tupfe sie trocken.", Step_EN = "Rinse the eggplant briefly and pat dry.", Step_PRT = "Enxágue a berinjela rapidamente e seque.", Step_ESP = "Enjuaga la berenjena y sécala." },
                new() { Step_DE = "Schneide die Tomaten in kleine Würfel.", Step_EN = "Dice the tomatoes into small cubes.", Step_PRT = "Corte os tomates em cubos pequenos.", Step_ESP = "Corta los tomates en cubitos." },
                new() { Step_DE = "Entkerne die Tomaten für eine mildere Sauce.", Step_EN = "Remove tomato seeds for a milder sauce.", Step_PRT = "Retire as sementes dos tomates para um molho mais suave.", Step_ESP = "Retira las semillas de los tomates para una salsa más suave." },
                new() { Step_DE = "Lege die Tomatenwürfel in ein Sieb und salze sie leicht.", Step_EN = "Place the diced tomatoes in a sieve and lightly salt them.", Step_PRT = "Coloque os tomates em cubos numa peneira e salgue levemente.", Step_ESP = "Coloca los tomates en cubos en un colador y sala ligeramente." },
                new() { Step_DE = "Schneide die Karotten in kleine Würfel.", Step_EN = "Dice the carrots into small cubes.", Step_PRT = "Corte as cenouras em cubos pequenos.", Step_ESP = "Corta las zanahorias en cubitos." },
                new() { Step_DE = "Schneide den Sellerie in feine Würfel.", Step_EN = "Dice the celery finely.", Step_PRT = "Pique o aipo finamente.", Step_ESP = "Pica el apio finamente." },
                new() { Step_DE = "Schneide den Lauch in feine Ringe.", Step_EN = "Slice the leek into thin rings.", Step_PRT = "Fatie o alho-poró em rodelas finas.", Step_ESP = "Corta el puerro en aros finos." },
                new() { Step_DE = "Spüle den Lauch in kaltem Wasser, um Sand zu entfernen.", Step_EN = "Rinse the leek in cold water to remove sand.", Step_PRT = "Enxágue o alho-poró em água fria para remover areia.", Step_ESP = "Enjuaga el puerro en agua fría para quitar arena." },
                new() { Step_DE = "Schneide die Kartoffeln in Spalten.", Step_EN = "Cut the potatoes into wedges.", Step_PRT = "Corte as batatas em gomos.", Step_ESP = "Corta las patatas en gajos." },
                new() { Step_DE = "Lege die Kartoffelspalten in kaltes Wasser.", Step_EN = "Place the potato wedges in cold water.", Step_PRT = "Coloque os gomos de batata em água fria.", Step_ESP = "Pon los gajos de patata en agua fría." },
                new() { Step_DE = "Trockne die Kartoffelspalten gründlich ab.", Step_EN = "Dry the potato wedges thoroughly.", Step_PRT = "Seque bem os gomos de batata.", Step_ESP = "Seca bien los gajos de patata." },
                new() { Step_DE = "Mische die Kartoffelspalten mit Öl und Gewürzen.", Step_EN = "Toss the potato wedges with oil and spices.", Step_PRT = "Misture os gomos de batata com óleo e especiarias.", Step_ESP = "Mezcla los gajos de patata con aceite y especias." },
                new() { Step_DE = "Heize die Pfanne vor, bevor du das Gemüse hineingibst.", Step_EN = "Preheat the pan before adding the vegetables.", Step_PRT = "Pré-aqueça a frigideira antes de adicionar os legumes.", Step_ESP = "Precalienta la sartén antes de añadir las verduras." },
                new() { Step_DE = "Schneide das Hähnchen in gleichmäßige Stücke.", Step_EN = "Cut the chicken into even pieces.", Step_PRT = "Corte o frango em pedaços uniformes.", Step_ESP = "Corta el pollo en trozos uniformes." },
                new() { Step_DE = "Tupfe das Hähnchen trocken und würze es.", Step_EN = "Pat the chicken dry and season it.", Step_PRT = "Seque o frango e tempere.", Step_ESP = "Seca el pollo y sazónalo." },
                new() { Step_DE = "Brate das Hähnchen goldbraun an.", Step_EN = "Sear the chicken until golden brown.", Step_PRT = "Dore o frango até ficar dourado.", Step_ESP = "Dora el pollo hasta que esté dorado." },
                new() { Step_DE = "Nimm das Hähnchen aus der Pfanne und stelle es beiseite.", Step_EN = "Remove the chicken from the pan and set aside.", Step_PRT = "Retire o frango da frigideira e reserve.", Step_ESP = "Retira el pollo de la sartén y reserva." },
                new() { Step_DE = "Gib das Gemüse in die Pfanne und brate es an.", Step_EN = "Add the vegetables to the pan and sauté them.", Step_PRT = "Adicione os legumes à frigideira e salteie.", Step_ESP = "Añade las verduras a la sartén y sofríelas." },
                new() { Step_DE = "Lösche mit Brühe ab und rühre den Boden frei.", Step_EN = "Deglaze with broth and scrape the bottom.", Step_PRT = "Deglace com caldo e raspe o fundo.", Step_ESP = "Desglasa con caldo y raspa el fondo." },
                new() { Step_DE = "Lass die Flüssigkeit um ein Drittel einkochen.", Step_EN = "Reduce the liquid by one third.", Step_PRT = "Deixe o líquido reduzir em um terço.", Step_ESP = "Reduce el líquido a un tercio." },
                new() { Step_DE = "Füge die Kräuter erst zum Schluss hinzu.", Step_EN = "Add the herbs only at the end.", Step_PRT = "Adicione as ervas somente no final.", Step_ESP = "Añade las hierbas al final." },
                new() { Step_DE = "Rühre Joghurt glatt, bevor du ihn zur Sauce gibst.", Step_EN = "Stir the yogurt smooth before adding to the sauce.", Step_PRT = "Misture o iogurte até ficar liso antes de adicionar ao molho.", Step_ESP = "Remueve el yogur hasta que esté suave antes de añadirlo." },
                new() { Step_DE = "Verrühre Senf mit Brühe für eine schnelle Sauce.", Step_EN = "Whisk mustard with broth for a quick sauce.", Step_PRT = "Bata mostarda com caldo para um molho rápido.", Step_ESP = "Bate la mostaza con caldo para una salsa rápida." },
                new() { Step_DE = "Rühre die Marinade an und stelle sie kalt.", Step_EN = "Mix the marinade and chill it.", Step_PRT = "Misture a marinada e leve à geladeira.", Step_ESP = "Mezcla la marinada y enfríala." },
                new() { Step_DE = "Lege das Fleisch in die Marinade und wende es.", Step_EN = "Place the meat in the marinade and turn it.", Step_PRT = "Coloque a carne na marinada e vire.", Step_ESP = "Coloca la carne en la marinada y dale la vuelta." },
                new() { Step_DE = "Ziehe die Marinade vor dem Braten gut ab.", Step_EN = "Let the marinade drip off before frying.", Step_PRT = "Deixe a marinada escorrer antes de fritar.", Step_ESP = "Deja escurrir la marinada antes de freír." },
                new() { Step_DE = "Schneide den Fisch in große Stücke für die Pfanne.", Step_EN = "Cut the fish into large pieces for the pan.", Step_PRT = "Corte o peixe em pedaços grandes para a frigideira.", Step_ESP = "Corta el pescado en trozos grandes para la sartén." },
                new() { Step_DE = "Würze den Fisch mit Salz und Zitronenpfeffer.", Step_EN = "Season the fish with salt and lemon pepper.", Step_PRT = "Tempere o peixe com sal e pimenta-limão.", Step_ESP = "Sazona el pescado con sal y pimienta limón." },
                new() { Step_DE = "Wende den Fisch nur einmal, damit er nicht zerfällt.", Step_EN = "Flip the fish only once so it doesn’t fall apart.", Step_PRT = "Vire o peixe apenas uma vez para não desmanchar.", Step_ESP = "Da la vuelta al pescado solo una vez para que no se rompa." },
                new() { Step_DE = "Lege den Fisch zum Nachziehen kurz beiseite.", Step_EN = "Let the fish rest briefly to finish cooking.", Step_PRT = "Deixe o peixe descansar um pouco para terminar de cozinhar.", Step_ESP = "Deja reposar el pescado para que termine de cocinarse." },
                new() { Step_DE = "Schneide den Tofu in Würfel und presse ihn leicht.", Step_EN = "Cube the tofu and press it lightly.", Step_PRT = "Corte o tofu em cubos e pressione levemente.", Step_ESP = "Corta el tofu en cubos y presiónalo ligeramente." },
                new() { Step_DE = "Mariniere den Tofu und lasse ihn 15 Minuten ziehen.", Step_EN = "Marinate the tofu and let it sit for 15 minutes.", Step_PRT = "Marine o tofu e deixe descansar por 15 minutos.", Step_ESP = "Marina el tofu y deja reposar 15 minutos." },
                new() { Step_DE = "Brate den Tofu rundherum knusprig.", Step_EN = "Pan-fry the tofu until crisp on all sides.", Step_PRT = "Frite o tofu até ficar crocante em todos os lados.", Step_ESP = "Fríe el tofu hasta que esté crujiente por todos lados." },
                new() { Step_DE = "Schneide den Halloumi in Scheiben.", Step_EN = "Slice the halloumi.", Step_PRT = "Fatie o halloumi.", Step_ESP = "Corta el halloumi en rodajas." },
                new() { Step_DE = "Brate den Halloumi kurz von beiden Seiten.", Step_EN = "Sear the halloumi briefly on both sides.", Step_PRT = "Doure o halloumi rapidamente dos dois lados.", Step_ESP = "Dora el halloumi brevemente por ambos lados." },
                new() { Step_DE = "Schneide die Kichererbsen vom Sud ab und spüle sie.", Step_EN = "Drain and rinse the chickpeas.", Step_PRT = "Escorra e enxágue o grão-de-bico.", Step_ESP = "Escurre y enjuaga los garbanzos." },
                new() { Step_DE = "Röste die Kichererbsen im Ofen knusprig.", Step_EN = "Roast the chickpeas in the oven until crisp.", Step_PRT = "Asse o grão-de-bico no forno até ficar crocante.", Step_ESP = "Hornea los garbanzos hasta que estén crujientes." },
                new() { Step_DE = "Schäle die Avocado und schneide sie in Scheiben.", Step_EN = "Peel the avocado and slice it.", Step_PRT = "Descasque o abacate e fatie.", Step_ESP = "Pela el aguacate y córtalo en láminas." },
                new() { Step_DE = "Zerdrücke die Avocado mit Salz und Limette.", Step_EN = "Mash the avocado with salt and lime.", Step_PRT = "Amasse o abacate com sal e limão.", Step_ESP = "Machaca el aguacate con sal y lima." },
                new() { Step_DE = "Schneide den Mais vom Kolben.", Step_EN = "Cut the corn kernels off the cob.", Step_PRT = "Retire os grãos de milho da espiga.", Step_ESP = "Corta los granos de maíz de la mazorca." },
                new() { Step_DE = "Röste den Mais kurz in der Pfanne.", Step_EN = "Toast the corn briefly in a pan.", Step_PRT = "Toste o milho rapidamente na frigideira.", Step_ESP = "Tuesta el maíz brevemente en la sartén." },
                new() { Step_DE = "Schneide den Lauch in feine Streifen.", Step_EN = "Cut the leek into thin strips.", Step_PRT = "Corte o alho-poró em tiras finas.", Step_ESP = "Corta el puerro en tiras finas." },
                new() { Step_DE = "Schneide den Kohl in feine Streifen.", Step_EN = "Shred the cabbage into thin strips.", Step_PRT = "Fatie o repolho em tiras finas.", Step_ESP = "Corta la col en tiras finas." },
                new() { Step_DE = "Massiere den Kohl mit Salz, bis er weich wird.", Step_EN = "Massage the cabbage with salt until it softens.", Step_PRT = "Massageie o repolho com sal até amolecer.", Step_ESP = "Masajea la col con sal hasta que se ablande." },
                new() { Step_DE = "Schneide die Radieschen in dünne Scheiben.", Step_EN = "Slice the radishes thinly.", Step_PRT = "Fatie os rabanetes finamente.", Step_ESP = "Corta los rábanos en láminas finas." },
                new() { Step_DE = "Schneide die Gurke in Stifte.", Step_EN = "Cut the cucumber into sticks.", Step_PRT = "Corte o pepino em palitos.", Step_ESP = "Corta el pepino en bastones." },
                new() { Step_DE = "Schneide den Salat in feine Streifen.", Step_EN = "Cut the lettuce into thin strips.", Step_PRT = "Corte a alface em tiras finas.", Step_ESP = "Corta la lechuga en tiras finas." },
                new() { Step_DE = "Verrühre Essig, Öl und Senf zu einer Vinaigrette.", Step_EN = "Whisk vinegar, oil, and mustard into a vinaigrette.", Step_PRT = "Bata vinagre, óleo e mostarda para uma vinagrete.", Step_ESP = "Bate vinagre, aceite y mostaza para una vinagreta." },
                new() { Step_DE = "Schmecke die Vinaigrette mit Honig ab.", Step_EN = "Balance the vinaigrette with a touch of honey.", Step_PRT = "Equilibre a vinagrete com um toque de mel.", Step_ESP = "Ajusta la vinagreta con un toque de miel." },
                new() { Step_DE = "Mische das Dressing kurz vor dem Servieren unter.", Step_EN = "Toss in the dressing just before serving.", Step_PRT = "Misture o molho pouco antes de servir.", Step_ESP = "Mezcla el aderezo justo antes de servir." },
                new() { Step_DE = "Röste die Nüsse kurz, bis sie duften.", Step_EN = "Toast the nuts briefly until fragrant.", Step_PRT = "Toste as nozes rapidamente até perfumar.", Step_ESP = "Tuesta los frutos secos hasta que desprendan aroma." },
                new() { Step_DE = "Hacke die Nüsse grob für das Topping.", Step_EN = "Roughly chop the nuts for topping.", Step_PRT = "Pique as nozes grosseiramente para a cobertura.", Step_ESP = "Pica los frutos secos en trozos grandes para cubrir." },
                new() { Step_DE = "Zerstoße die Gewürze im Mörser.", Step_EN = "Crush the spices in a mortar.", Step_PRT = "Esmague as especiarias no almofariz.", Step_ESP = "Muele las especias en un mortero." },
                new() { Step_DE = "Röste die Gewürze kurz in der Pfanne.", Step_EN = "Toast the spices briefly in a pan.", Step_PRT = "Toste as especiarias rapidamente na frigideira.", Step_ESP = "Tuesta las especias brevemente en la sartén." },
                new() { Step_DE = "Verrühre Gewürze mit Öl zu einer Gewürzpaste.", Step_EN = "Mix spices with oil to form a paste.", Step_PRT = "Misture especiarias com óleo para formar uma pasta.", Step_ESP = "Mezcla especias con aceite para formar una pasta." },
                new() { Step_DE = "Schneide die Zitrone in feine Scheiben.", Step_EN = "Slice the lemon into thin rounds.", Step_PRT = "Fatie o limão em rodelas finas.", Step_ESP = "Corta el limón en rodajas finas." },
                new() { Step_DE = "Lege Zitronenscheiben auf das Gericht zum Aromatisieren.", Step_EN = "Lay lemon slices on the dish for aroma.", Step_PRT = "Coloque rodelas de limão sobre o prato para aromatizar.", Step_ESP = "Coloca rodajas de limón sobre el plato para aromatizar." },
                new() { Step_DE = "Rühre die Sauce mit einem Schneebesen glatt.", Step_EN = "Whisk the sauce until smooth.", Step_PRT = "Bata o molho com um fouet até ficar liso.", Step_ESP = "Bate la salsa hasta que quede suave." },
                new() { Step_DE = "Lass die Sauce ohne Deckel reduzieren.", Step_EN = "Let the sauce reduce uncovered.", Step_PRT = "Deixe o molho reduzir sem tampa.", Step_ESP = "Deja que la salsa reduzca sin tapa." },
                new() { Step_DE = "Schmecke die Sauce mit einem Schuss Zitronensaft ab.", Step_EN = "Finish the sauce with a splash of lemon juice.", Step_PRT = "Finalize o molho com um toque de suco de limão.", Step_ESP = "Termina la salsa con un chorrito de jugo de limón." },
                new() { Step_DE = "Rühre Crème fraîche in die Sauce für mehr Fülle.", Step_EN = "Stir crème fraîche into the sauce for richness.", Step_PRT = "Misture crème fraîche no molho para mais cremosidade.", Step_ESP = "Incorpora crème fraîche a la salsa para más cuerpo." },
                new() { Step_DE = "Stelle die Sauce warm, ohne sie zu kochen.", Step_EN = "Keep the sauce warm without boiling.", Step_PRT = "Mantenha o molho aquecido sem ferver.", Step_ESP = "Mantén la salsa caliente sin hervir." },
                new() { Step_DE = "Rühre die Polenta in den kochenden Sud ein.", Step_EN = "Whisk the polenta into the boiling liquid.", Step_PRT = "Misture a polenta no líquido fervente.", Step_ESP = "Incorpora la polenta al líquido hirviendo." },
                new() { Step_DE = "Lass die Polenta unter Rühren eindicken.", Step_EN = "Stir the polenta until it thickens.", Step_PRT = "Mexa a polenta até engrossar.", Step_ESP = "Remueve la polenta hasta que espese." },
                new() { Step_DE = "Streue Käse in die Polenta für mehr Geschmack.", Step_EN = "Stir cheese into the polenta for more flavor.", Step_PRT = "Misture queijo na polenta para mais sabor.", Step_ESP = "Añade queso a la polenta para más sabor." },
                new() { Step_DE = "Schneide die Polenta in Scheiben und brate sie an.", Step_EN = "Slice the polenta and pan-fry it.", Step_PRT = "Corte a polenta em fatias e frite.", Step_ESP = "Corta la polenta en rodajas y fríela." },
                new() { Step_DE = "Schneide die Zwiebeln in feine Halbmonde.", Step_EN = "Slice the onions into thin half-moons.", Step_PRT = "Fatie as cebolas em meias-luas finas.", Step_ESP = "Corta las cebollas en medias lunas finas." },
                new() { Step_DE = "Karamellisiere die Zwiebeln langsam.", Step_EN = "Caramelize the onions slowly.", Step_PRT = "Caramelize as cebolas lentamente.", Step_ESP = "Carameliza las cebollas lentamente." },
                new() { Step_DE = "Lösche die Zwiebeln mit Balsamico ab.", Step_EN = "Deglaze the onions with balsamic.", Step_PRT = "Deglace as cebolas com balsâmico.", Step_ESP = "Desglasa las cebollas con balsámico." },
                new() { Step_DE = "Schneide den Speck in feine Streifen.", Step_EN = "Cut the bacon into thin strips.", Step_PRT = "Corte o bacon em tiras finas.", Step_ESP = "Corta el bacon en tiras finas." },
                new() { Step_DE = "Brate den Speck knusprig und stelle ihn beiseite.", Step_EN = "Fry the bacon until crisp and set aside.", Step_PRT = "Frite o bacon até ficar crocante e reserve.", Step_ESP = "Fríe el bacon hasta que quede crujiente y reserva." },
                new() { Step_DE = "Gib den Speck am Ende wieder hinzu.", Step_EN = "Add the bacon back in at the end.", Step_PRT = "Adicione o bacon novamente no final.", Step_ESP = "Añade el bacon al final." },
                new() { Step_DE = "Schneide die Wurst in Scheiben und brate sie an.", Step_EN = "Slice the sausage and sear it.", Step_PRT = "Fatie a linguiça e doure.", Step_ESP = "Corta la salchicha y dórela." },
                new() { Step_DE = "Gib die Wurst erst kurz vor dem Servieren dazu.", Step_EN = "Add the sausage shortly before serving.", Step_PRT = "Adicione a linguiça pouco antes de servir.", Step_ESP = "Añade la salchicha poco antes de servir." },
                new() { Step_DE = "Schneide den Reis an, bevor du ihn anbrätst.", Step_EN = "Rinse the rice before toasting it.", Step_PRT = "Lave o arroz antes de tostá-lo.", Step_ESP = "Lava el arroz antes de tostarlo." },
                new() { Step_DE = "Röste den Reis kurz, bevor du Flüssigkeit zugibst.", Step_EN = "Toast the rice briefly before adding liquid.", Step_PRT = "Toste o arroz brevemente antes de adicionar líquido.", Step_ESP = "Tuesta el arroz antes de añadir líquido." },
                new() { Step_DE = "Gib die Brühe nach und nach zum Reis.", Step_EN = "Add the broth to the rice gradually.", Step_PRT = "Adicione o caldo ao arroz aos poucos.", Step_ESP = "Añade el caldo al arroz poco a poco." },
                new() { Step_DE = "Rühre das Risotto regelmäßig um.", Step_EN = "Stir the risotto regularly.", Step_PRT = "Mexa o risoto regularmente.", Step_ESP = "Remueve el risotto con frecuencia." },
                new() { Step_DE = "Rühre am Ende Butter und Käse ein.", Step_EN = "Stir in butter and cheese at the end.", Step_PRT = "Misture manteiga e queijo no final.", Step_ESP = "Incorpora mantequilla y queso al final." },
                new() { Step_DE = "Lege das Backpapier in die Form.", Step_EN = "Line the pan with baking paper.", Step_PRT = "Forre a forma com papel manteiga.", Step_ESP = "Forra el molde con papel de hornear." },
                new() { Step_DE = "Fette die Form zusätzlich leicht ein.", Step_EN = "Lightly grease the pan as well.", Step_PRT = "Unte levemente a forma também.", Step_ESP = "Engrasa ligeramente el molde también." },
                new() { Step_DE = "Verrühre die trockenen Zutaten in einer Schüssel.", Step_EN = "Mix the dry ingredients in a bowl.", Step_PRT = "Misture os ingredientes secos numa tigela.", Step_ESP = "Mezcla los ingredientes secos en un bol." },
                new() { Step_DE = "Rühre die flüssigen Zutaten separat glatt.", Step_EN = "Whisk the wet ingredients separately until smooth.", Step_PRT = "Bata os ingredientes líquidos separadamente até ficar liso.", Step_ESP = "Bate los ingredientes líquidos por separado hasta que estén suaves." },
                new() { Step_DE = "Hebe die trockenen Zutaten unter die flüssigen.", Step_EN = "Fold the dry ingredients into the wet.", Step_PRT = "Incorpore os ingredientes secos aos líquidos.", Step_ESP = "Incorpora los ingredientes secos a los líquidos." },
                new() { Step_DE = "Rühre den Teig nur kurz, damit er locker bleibt.", Step_EN = "Stir the batter briefly to keep it light.", Step_PRT = "Misture a massa rapidamente para ficar leve.", Step_ESP = "Mezcla la masa brevemente para que quede esponjosa." },
                new() { Step_DE = "Fülle den Teig in die Form und streiche ihn glatt.", Step_EN = "Pour the batter into the pan and smooth the top.", Step_PRT = "Despeje a massa na forma e alise.", Step_ESP = "Vierte la masa en el molde y alisa." },
                new() { Step_DE = "Backe den Teig bis er goldbraun ist.", Step_EN = "Bake until the top is golden brown.", Step_PRT = "Asse até dourar.", Step_ESP = "Hornea hasta que esté dorado." },
                new() { Step_DE = "Lass das Gebäck kurz auskühlen.", Step_EN = "Let the baked goods cool briefly.", Step_PRT = "Deixe o assado esfriar um pouco.", Step_ESP = "Deja que el horneado se enfríe un poco." },
                new() { Step_DE = "Stürze den Kuchen vorsichtig auf ein Gitter.", Step_EN = "Carefully turn the cake onto a rack.", Step_PRT = "Desenforme o bolo com cuidado sobre uma grade.", Step_ESP = "Desmolda el pastel con cuidado sobre una rejilla." },
                new() { Step_DE = "Bestreiche das Gebäck mit Glasur.", Step_EN = "Brush the pastry with glaze.", Step_PRT = "Pincele o doce com glacê.", Step_ESP = "Pincela el pastel con glaseado." },
                new() { Step_DE = "Streue Puderzucker über das Gebäck.", Step_EN = "Dust the pastry with powdered sugar.", Step_PRT = "Polvilhe açúcar de confeiteiro sobre o doce.", Step_ESP = "Espolvorea azúcar glas sobre el pastel." },
                new() { Step_DE = "Schneide das Brot in dicke Scheiben.", Step_EN = "Slice the bread into thick slices.", Step_PRT = "Corte o pão em fatias grossas.", Step_ESP = "Corta el pan en rebanadas gruesas." },
                new() { Step_DE = "Röste das Brot in der Pfanne an.", Step_EN = "Toast the bread in a pan.", Step_PRT = "Toste o pão na frigideira.", Step_ESP = "Tuesta el pan en la sartén." },
                new() { Step_DE = "Reibe das Brot mit Knoblauch ein.", Step_EN = "Rub the bread with garlic.", Step_PRT = "Esfregue o pão com alho.", Step_ESP = "Frota el pan con ajo." },
                new() { Step_DE = "Träufle Olivenöl über das Brot.", Step_EN = "Drizzle olive oil over the bread.", Step_PRT = "Regue o pão com azeite.", Step_ESP = "Rocía el pan con aceite de oliva." },
                new() { Step_DE = "Belege das Brot mit Tomaten und Kräutern.", Step_EN = "Top the bread with tomatoes and herbs.", Step_PRT = "Cubra o pão com tomate e ervas.", Step_ESP = "Cubre el pan con tomate y hierbas." },
                new() { Step_DE = "Schneide die Kartoffeln in Scheiben für Gratin.", Step_EN = "Slice the potatoes for gratin.", Step_PRT = "Fatie as batatas para gratinar.", Step_ESP = "Corta las patatas en rodajas para gratinar." },
                new() { Step_DE = "Schichte die Kartoffeln in der Form.", Step_EN = "Layer the potatoes in the dish.", Step_PRT = "Faça camadas de batata na forma.", Step_ESP = "Coloca las patatas en capas en la fuente." },
                new() { Step_DE = "Gieße Sahne über die Kartoffeln.", Step_EN = "Pour cream over the potatoes.", Step_PRT = "Despeje creme sobre as batatas.", Step_ESP = "Vierte nata sobre las patatas." },
                new() { Step_DE = "Streue Käse über die Gratin-Schichten.", Step_EN = "Sprinkle cheese over the gratin layers.", Step_PRT = "Polvilhe queijo sobre as camadas do gratin.", Step_ESP = "Espolvorea queso sobre las capas del gratinado." },
                new() { Step_DE = "Backe das Gratin, bis es blubbert.", Step_EN = "Bake the gratin until bubbly.", Step_PRT = "Asse o gratin até borbulhar.", Step_ESP = "Hornea el gratinado hasta que burbujee." },
                new() { Step_DE = "Lass das Gratin kurz ruhen, bevor du es anschneidest.", Step_EN = "Let the gratin rest before slicing.", Step_PRT = "Deixe o gratin descansar antes de cortar.", Step_ESP = "Deja reposar el gratinado antes de cortar." },
                new() { Step_DE = "Schäle die Zwiebeln für das Ragout.", Step_EN = "Peel the onions for the ragout.", Step_PRT = "Descasque as cebolas para o ragu.", Step_ESP = "Pela las cebollas para el ragú." },
                new() { Step_DE = "Schneide die Zwiebeln in kleine Würfel.", Step_EN = "Dice the onions into small cubes.", Step_PRT = "Corte as cebolas em cubos pequenos.", Step_ESP = "Corta las cebollas en cubitos." },
                new() { Step_DE = "Brate das Ragout bei mittlerer Hitze an.", Step_EN = "Sauté the ragout over medium heat.", Step_PRT = "Refogue o ragu em fogo médio.", Step_ESP = "Sofríe el ragú a fuego medio." },
                new() { Step_DE = "Lass das Ragout langsam einkochen.", Step_EN = "Let the ragout reduce slowly.", Step_PRT = "Deixe o ragu reduzir lentamente.", Step_ESP = "Deja que el ragú reduzca lentamente." },
                new() { Step_DE = "Schmecke das Ragout mit Kräutern ab.", Step_EN = "Season the ragout with herbs.", Step_PRT = "Tempere o ragu com ervas.", Step_ESP = "Sazona el ragú con hierbas." },
                new() { Step_DE = "Stelle das Ragout warm, bis du servierst.", Step_EN = "Keep the ragout warm until serving.", Step_PRT = "Mantenha o ragu aquecido até servir.", Step_ESP = "Mantén el ragú caliente hasta servir." },
                new() { Step_DE = "Schneide die Bohnen in kurze Stücke.", Step_EN = "Cut the beans into short pieces.", Step_PRT = "Corte o feijão verde em pedaços curtos.", Step_ESP = "Corta las judías en trozos cortos." },
                new() { Step_DE = "Blanchiere die Bohnen kurz.", Step_EN = "Blanch the beans briefly.", Step_PRT = "Branqueie o feijão rapidamente.", Step_ESP = "Escalda las judías brevemente." },
                new() { Step_DE = "Schwenke die Bohnen in Butter oder Öl.", Step_EN = "Toss the beans in butter or oil.", Step_PRT = "Salteie o feijão na manteiga ou óleo.", Step_ESP = "Saltea las judías en mantequilla o aceite." },
                new() { Step_DE = "Bestreue die Bohnen mit gerösteten Mandeln.", Step_EN = "Sprinkle the beans with toasted almonds.", Step_PRT = "Polvilhe o feijão com amêndoas tostadas.", Step_ESP = "Espolvorea las judías con almendras tostadas." },
                new() { Step_DE = "Schneide die Mandeln in Blättchen.", Step_EN = "Slice the almonds into flakes.", Step_PRT = "Fatie as amêndoas em lâminas.", Step_ESP = "Corta las almendras en láminas." },
                new() { Step_DE = "Röste die Mandeln bis sie duften.", Step_EN = "Toast the almonds until fragrant.", Step_PRT = "Toste as amêndoas até perfumar.", Step_ESP = "Tuesta las almendras hasta que perfumen." },
                new() { Step_DE = "Schäle die Äpfel und schneide sie in Spalten.", Step_EN = "Peel the apples and cut into wedges.", Step_PRT = "Descasque as maçãs e corte em gomos.", Step_ESP = "Pela las manzanas y córtalas en gajos." },
                new() { Step_DE = "Brate die Apfelspalten kurz in Butter an.", Step_EN = "Sauté the apple wedges briefly in butter.", Step_PRT = "Salteie os gomos de maçã rapidamente na manteiga.", Step_ESP = "Saltea los gajos de manzana en mantequilla." },
                new() { Step_DE = "Bestreue die Äpfel mit Zimt und Zucker.", Step_EN = "Sprinkle the apples with cinnamon and sugar.", Step_PRT = "Polvilhe as maçãs com canela e açúcar.", Step_ESP = "Espolvorea las manzanas con canela y azúcar." },
                new() { Step_DE = "Schneide die Bananen in Scheiben.", Step_EN = "Slice the bananas.", Step_PRT = "Fatie as bananas.", Step_ESP = "Corta los plátanos en rodajas." },
                new() { Step_DE = "Schmelze die Schokolade langsam.", Step_EN = "Melt the chocolate slowly.", Step_PRT = "Derreta o chocolate lentamente.", Step_ESP = "Derrite el chocolate lentamente." },
                new() { Step_DE = "Rühre die geschmolzene Schokolade glatt.", Step_EN = "Stir the melted chocolate smooth.", Step_PRT = "Misture o chocolate derretido até ficar liso.", Step_ESP = "Remueve el chocolate derretido hasta que quede suave." },
                new() { Step_DE = "Tauche die Früchte in die Schokolade.", Step_EN = "Dip the fruit into the chocolate.", Step_PRT = "Mergulhe as frutas no chocolate.", Step_ESP = "Sumerge la fruta en el chocolate." },
                new() { Step_DE = "Lasse die Schokolade auf Backpapier fest werden.", Step_EN = "Let the chocolate set on parchment paper.", Step_PRT = "Deixe o chocolate firmar sobre papel manteiga.", Step_ESP = "Deja que el chocolate se endurezca sobre papel." },
                new() { Step_DE = "Zerkleinere die Nüsse grob für den Teig.", Step_EN = "Roughly chop the nuts for the batter.", Step_PRT = "Pique as nozes grosseiramente para a massa.", Step_ESP = "Pica los frutos secos en trozos grandes para la masa." },
                new() { Step_DE = "Hebe die Nüsse vorsichtig unter den Teig.", Step_EN = "Fold the nuts into the batter gently.", Step_PRT = "Incorpore as nozes delicadamente na massa.", Step_ESP = "Incorpora los frutos secos con suavidad a la masa." },
                new() { Step_DE = "Teile den Teig in Förmchen.", Step_EN = "Divide the batter into molds.", Step_PRT = "Divida a massa em forminhas.", Step_ESP = "Reparte la masa en moldes." },
                new() { Step_DE = "Backe die Muffins bis sie durchgebacken sind.", Step_EN = "Bake the muffins until done.", Step_PRT = "Asse os muffins até ficarem prontos.", Step_ESP = "Hornea los muffins hasta que estén hechos." },
                new() { Step_DE = "Stich mit einem Holzstäbchen, ob der Teig gar ist.", Step_EN = "Use a skewer to check if the batter is cooked.", Step_PRT = "Use um palito para verificar se a massa está assada.", Step_ESP = "Usa un palillo para comprobar si está hecho." },
                new() { Step_DE = "Lass die Muffins auf einem Gitter auskühlen.", Step_EN = "Let the muffins cool on a rack.", Step_PRT = "Deixe os muffins esfriarem numa grade.", Step_ESP = "Deja enfriar los muffins en una rejilla." },
                new() { Step_DE = "Schäle die Zitrusfrüchte großzügig.", Step_EN = "Peel the citrus fruits generously.", Step_PRT = "Descasque as frutas cítricas generosamente.", Step_ESP = "Pela los cítricos con cuidado." },
                new() { Step_DE = "Filetiere die Zitrusfrüchte.", Step_EN = "Segment the citrus fruits.", Step_PRT = "Filete as frutas cítricas.", Step_ESP = "Filetea los cítricos." },
                new() { Step_DE = "Fange den Saft beim Filetieren auf.", Step_EN = "Catch the juice while segmenting.", Step_PRT = "Aproveite o suco ao filetar.", Step_ESP = "Recoge el jugo al filetear." },
                new() { Step_DE = "Vermische den Saft mit Honig als Dressing.", Step_EN = "Mix the juice with honey as a dressing.", Step_PRT = "Misture o suco com mel como molho.", Step_ESP = "Mezcla el jugo con miel como aderezo." },
                new() { Step_DE = "Mische die Zitrusfilets mit dem Dressing.", Step_EN = "Toss the citrus segments with the dressing.", Step_PRT = "Misture os filetes cítricos com o molho.", Step_ESP = "Mezcla los gajos de cítricos con el aderezo." },
                new() { Step_DE = "Schneide die Kohlrabi in feine Stifte.", Step_EN = "Cut the kohlrabi into thin sticks.", Step_PRT = "Corte o kohlrabi em palitos finos.", Step_ESP = "Corta el colirrábano en bastones finos." },
                new() { Step_DE = "Schneide den Kürbis in dünne Spalten.", Step_EN = "Cut the pumpkin into thin wedges.", Step_PRT = "Corte a abóbora em gomos finos.", Step_ESP = "Corta la calabaza en gajos finos." },
                new() { Step_DE = "Bestreiche den Kürbis mit Öl und würze ihn.", Step_EN = "Brush the pumpkin with oil and season it.", Step_PRT = "Pincele a abóbora com óleo e tempere.", Step_ESP = "Unta la calabaza con aceite y sazónala." },
                new() { Step_DE = "Röste den Kürbis im Ofen, bis er weich ist.", Step_EN = "Roast the pumpkin in the oven until tender.", Step_PRT = "Asse a abóbora no forno até ficar macia.", Step_ESP = "Asa la calabaza hasta que esté tierna." },
                new() { Step_DE = "Schneide die rote Zwiebel in feine Ringe.", Step_EN = "Slice the red onion into thin rings.", Step_PRT = "Fatie a cebola roxa em rodelas finas.", Step_ESP = "Corta la cebolla morada en aros finos." },
                new() { Step_DE = "Lege die Zwiebelringe in Essig ein.", Step_EN = "Pickle the onion rings in vinegar.", Step_PRT = "Faça picles das rodelas de cebola no vinagre.", Step_ESP = "Encúrte los aros de cebolla en vinagre." },
                new() { Step_DE = "Lass die Zwiebeln 10 Minuten ziehen.", Step_EN = "Let the onions sit for 10 minutes.", Step_PRT = "Deixe as cebolas descansar por 10 minutos.", Step_ESP = "Deja reposar las cebollas 10 minutos." },
                new() { Step_DE = "Schneide die Peperoni fein.", Step_EN = "Finely chop the chili.", Step_PRT = "Pique a pimenta finamente.", Step_ESP = "Pica el chile finamente." },
                new() { Step_DE = "Rühre die Peperoni erst am Ende ein.", Step_EN = "Stir in the chili at the end.", Step_PRT = "Misture a pimenta no final.", Step_ESP = "Añade el chile al final." },
                new() { Step_DE = "Schneide den Ingwer in feine Streifen.", Step_EN = "Cut the ginger into thin strips.", Step_PRT = "Corte o gengibre em tiras finas.", Step_ESP = "Corta el jengibre en tiras finas." },
                new() { Step_DE = "Schneide den Ingwer in kleine Würfel.", Step_EN = "Dice the ginger into small cubes.", Step_PRT = "Corte o gengibre em cubos pequenos.", Step_ESP = "Corta el jengibre en cubitos." },
                new() { Step_DE = "Röste den Ingwer kurz an.", Step_EN = "Toast the ginger briefly.", Step_PRT = "Toste o gengibre rapidamente.", Step_ESP = "Tuesta el jengibre brevemente." },
                new() { Step_DE = "Gib Kokosmilch in die Sauce für mehr Cremigkeit.", Step_EN = "Add coconut milk to the sauce for more creaminess.", Step_PRT = "Adicione leite de coco ao molho para mais cremosidade.", Step_ESP = "Añade leche de coco a la salsa para más cremosidad." },
                new() { Step_DE = "Lass die Kokosmilch kurz aufkochen.", Step_EN = "Bring the coconut milk to a brief boil.", Step_PRT = "Ferva rapidamente o leite de coco.", Step_ESP = "Hierve brevemente la leche de coco." },
                new() { Step_DE = "Rühre die Sauce ständig, damit sie nicht ansetzt.", Step_EN = "Stir the sauce constantly so it doesn’t stick.", Step_PRT = "Mexa o molho constantemente para não grudar.", Step_ESP = "Remueve la salsa constantemente para que no se pegue." },
                new() { Step_DE = "Schneide den Mozzarella in Stücke.", Step_EN = "Cut the mozzarella into pieces.", Step_PRT = "Corte a mozzarella em pedaços.", Step_ESP = "Corta la mozzarella en trozos." },
                new() { Step_DE = "Verteile den Mozzarella auf dem Gericht.", Step_EN = "Scatter the mozzarella over the dish.", Step_PRT = "Distribua a mozzarella sobre o prato.", Step_ESP = "Distribuye la mozzarella sobre el plato." },
                new() { Step_DE = "Grilliere das Gericht kurz, bis der Käse schmilzt.", Step_EN = "Grill the dish briefly until the cheese melts.", Step_PRT = "Grelhe rapidamente até o queijo derreter.", Step_ESP = "Gratina brevemente hasta que el queso se derrita." },
                new() { Step_DE = "Schneide die Tortillas in Streifen.", Step_EN = "Cut the tortillas into strips.", Step_PRT = "Corte as tortillas em tiras.", Step_ESP = "Corta las tortillas en tiras." },
                new() { Step_DE = "Röste die Tortillastreifen im Ofen.", Step_EN = "Bake the tortilla strips in the oven.", Step_PRT = "Asse as tiras de tortilla no forno.", Step_ESP = "Hornea las tiras de tortilla." },
                new() { Step_DE = "Streue die Tortillastreifen über die Suppe.", Step_EN = "Sprinkle the tortilla strips over the soup.", Step_PRT = "Polvilhe as tiras de tortilla sobre a sopa.", Step_ESP = "Espolvorea las tiras de tortilla sobre la sopa." },
                new() { Step_DE = "Schneide den Kohlrabi in dünne Scheiben.", Step_EN = "Slice the kohlrabi thinly.", Step_PRT = "Fatie o kohlrabi finamente.", Step_ESP = "Corta el colirrábano en láminas finas." },
                new() { Step_DE = "Dünste den Kohlrabi kurz in Butter.", Step_EN = "Sauté the kohlrabi briefly in butter.", Step_PRT = "Refogue o kohlrabi rapidamente na manteiga.", Step_ESP = "Sofríe el colirrábano brevemente en mantequilla." },
                new() { Step_DE = "Gib Brühe hinzu und lasse den Kohlrabi gar ziehen.", Step_EN = "Add broth and let the kohlrabi cook through.", Step_PRT = "Adicione caldo e deixe o kohlrabi cozinhar.", Step_ESP = "Añade caldo y deja que el colirrábano se cocine." },
                new() { Step_DE = "Schneide die Mangoldblätter in Streifen.", Step_EN = "Cut the chard leaves into strips.", Step_PRT = "Corte as folhas de acelga em tiras.", Step_ESP = "Corta las hojas de acelga en tiras." },
                new() { Step_DE = "Trenne die Mangoldstiele und schneide sie klein.", Step_EN = "Separate the chard stems and dice them.", Step_PRT = "Separe os talos da acelga e pique.", Step_ESP = "Separa los tallos de la acelga y córtalos." },
                new() { Step_DE = "Gare die Mangoldstiele zuerst, danach die Blätter.", Step_EN = "Cook the chard stems first, then the leaves.", Step_PRT = "Cozinhe primeiro os talos, depois as folhas.", Step_ESP = "Cocina primero los tallos, luego las hojas." },
                new() { Step_DE = "Schneide die Enden vom Spargel ab.", Step_EN = "Trim the ends of the asparagus.", Step_PRT = "Apare as pontas do aspargo.", Step_ESP = "Corta las puntas de los espárragos." },
                new() { Step_DE = "Schneide den Spargel in Stücke.", Step_EN = "Cut the asparagus into pieces.", Step_PRT = "Corte o aspargo em pedaços.", Step_ESP = "Corta los espárragos en trozos." },
                new() { Step_DE = "Gare den Spargel kurz in Butter.", Step_EN = "Sauté the asparagus briefly in butter.", Step_PRT = "Salteie o aspargo rapidamente na manteiga.", Step_ESP = "Saltea los espárragos brevemente en mantequilla." },
                new() { Step_DE = "Schneide die Bohnen in schräge Stücke.", Step_EN = "Cut the beans into diagonal pieces.", Step_PRT = "Corte o feijão em pedaços diagonais.", Step_ESP = "Corta las judías en trozos diagonales." },
                new() { Step_DE = "Lass die Bohnen in Salzwasser bissfest garen.", Step_EN = "Cook the beans in salted water until tender-crisp.", Step_PRT = "Cozinhe o feijão em água salgada até ficar al dente.", Step_ESP = "Cocina las judías en agua con sal hasta que estén al dente." },
                new() { Step_DE = "Schwenke die Bohnen in Knoblauchöl.", Step_EN = "Toss the beans in garlic oil.", Step_PRT = "Salteie o feijão em óleo de alho.", Step_ESP = "Saltea las judías en aceite de ajo." },
                new() { Step_DE = "Schneide die Gurke in kleine Würfel für Salsa.", Step_EN = "Dice the cucumber for salsa.", Step_PRT = "Corte o pepino em cubos para a salsa.", Step_ESP = "Corta el pepino en cubos para la salsa." },
                new() { Step_DE = "Mische Gurke, Tomate und Zwiebel für eine frische Salsa.", Step_EN = "Mix cucumber, tomato, and onion for a fresh salsa.", Step_PRT = "Misture pepino, tomate e cebola para uma salsa fresca.", Step_ESP = "Mezcla pepino, tomate y cebolla para una salsa fresca." },
                new() { Step_DE = "Schmecke die Salsa mit Limettensaft ab.", Step_EN = "Season the salsa with lime juice.", Step_PRT = "Tempere a salsa com suco de limão.", Step_ESP = "Sazona la salsa con jugo de lima." },
                new() { Step_DE = "Stelle die Salsa kalt bis zum Servieren.", Step_EN = "Chill the salsa until serving.", Step_PRT = "Leve a salsa à geladeira até servir.", Step_ESP = "Enfría la salsa hasta servir." },
                new() { Step_DE = "Schneide den Rucola grob.", Step_EN = "Roughly chop the arugula.", Step_PRT = "Pique a rúcula grosseiramente.", Step_ESP = "Corta la rúcula en trozos grandes." },
                new() { Step_DE = "Gib den Rucola erst am Ende dazu.", Step_EN = "Add the arugula only at the end.", Step_PRT = "Adicione a rúcula somente no final.", Step_ESP = "Añade la rúcula al final." },
                new() { Step_DE = "Schneide den Parmesan in feine Späne.", Step_EN = "Shave the parmesan into thin flakes.", Step_PRT = "Faça lascas finas de parmesão.", Step_ESP = "Haz lascas finas de parmesano." },
                new() { Step_DE = "Streue die Parmesanspäne über das Gericht.", Step_EN = "Scatter the parmesan flakes over the dish.", Step_PRT = "Espalhe as lascas de parmesão sobre o prato.", Step_ESP = "Esparce las lascas de parmesano sobre el plato." },
                new() { Step_DE = "Schneide die Kräuterbutter in Stücke.", Step_EN = "Cut the herb butter into pieces.", Step_PRT = "Corte a manteiga de ervas em pedaços.", Step_ESP = "Corta la mantequilla de hierbas en trozos." },
                new() { Step_DE = "Gib die Kräuterbutter auf das heiße Gemüse.", Step_EN = "Place the herb butter on the hot vegetables.", Step_PRT = "Coloque a manteiga de ervas sobre os legumes quentes.", Step_ESP = "Pon la mantequilla de hierbas sobre las verduras calientes." },
                new() { Step_DE = "Schneide die Kartoffeln in gleichmäßige Würfel.", Step_EN = "Cut the potatoes into even cubes.", Step_PRT = "Corte as batatas em cubos uniformes.", Step_ESP = "Corta las patatas en cubos uniformes." },
                new() { Step_DE = "Koche die Kartoffelwürfel in Salzwasser.", Step_EN = "Boil the potato cubes in salted water.", Step_PRT = "Cozinhe os cubos de batata em água salgada.", Step_ESP = "Hierve los cubos de patata en agua con sal." },
                new() { Step_DE = "Gieße die Kartoffeln ab und lasse sie ausdampfen.", Step_EN = "Drain the potatoes and let them steam off.", Step_PRT = "Escorra as batatas e deixe evaporar.", Step_ESP = "Escurre las patatas y deja que se evaporen." },
                new() { Step_DE = "Zerdrücke die Kartoffeln zu Püree.", Step_EN = "Mash the potatoes into a puree.", Step_PRT = "Amasse as batatas para fazer purê.", Step_ESP = "Tritura las patatas para hacer puré." },
                new() { Step_DE = "Rühre Milch und Butter unter das Püree.", Step_EN = "Stir milk and butter into the mash.", Step_PRT = "Misture leite e manteiga ao purê.", Step_ESP = "Añade leche y mantequilla al puré." },
                new() { Step_DE = "Schmecke das Püree mit Muskat ab.", Step_EN = "Season the mash with nutmeg.", Step_PRT = "Tempere o purê com noz-moscada.", Step_ESP = "Sazona el puré con nuez moscada." },
                new() { Step_DE = "Schneide die Nudeln in eine Auflaufform.", Step_EN = "Transfer the pasta to a baking dish.", Step_PRT = "Coloque a massa numa travessa.", Step_ESP = "Pasa la pasta a una fuente para horno." },
                new() { Step_DE = "Vermische die Pasta mit der Sauce in der Form.", Step_EN = "Mix the pasta with the sauce in the dish.", Step_PRT = "Misture a massa com o molho na travessa.", Step_ESP = "Mezcla la pasta con la salsa en la fuente." },
                new() { Step_DE = "Bestreue die Pasta mit Käse für das Gratin.", Step_EN = "Sprinkle the pasta with cheese for gratin.", Step_PRT = "Polvilhe queijo sobre a massa para gratinar.", Step_ESP = "Espolvorea queso sobre la pasta para gratinar." },
                new() { Step_DE = "Backe den Auflauf bis die Oberfläche goldbraun ist.", Step_EN = "Bake the casserole until the top is golden.", Step_PRT = "Asse o gratinado até a superfície dourar.", Step_ESP = "Hornea el gratinado hasta que se dore." },
                new() { Step_DE = "Lass den Auflauf 5 Minuten ruhen.", Step_EN = "Let the casserole rest for 5 minutes.", Step_PRT = "Deixe o gratinado descansar 5 minutos.", Step_ESP = "Deja reposar el gratinado 5 minutos." },
                new() { Step_DE = "Schneide die Portionen mit einem scharfen Messer.", Step_EN = "Cut portions with a sharp knife.", Step_PRT = "Corte as porções com uma faca afiada.", Step_ESP = "Corta las porciones con un cuchillo afilado." },
                new() { Step_DE = "Rühre die Sauce mit einem Löffel glatt.", Step_EN = "Stir the sauce smooth with a spoon.", Step_PRT = "Misture o molho até ficar liso.", Step_ESP = "Remueve la salsa hasta que esté suave." },
                new() { Step_DE = "Schmecke das Gericht mit frisch gemahlenem Pfeffer ab.", Step_EN = "Finish the dish with freshly ground pepper.", Step_PRT = "Finalize o prato com pimenta moída na hora.", Step_ESP = "Termina el plato con pimienta recién molida." },
                new() { Step_DE = "Rühre etwas Olivenöl unter die fertige Pasta.", Step_EN = "Stir a little olive oil into the finished pasta.", Step_PRT = "Misture um pouco de azeite na massa pronta.", Step_ESP = "Añade un poco de aceite de oliva a la pasta." },
                new() { Step_DE = "Bestreue das Gericht mit geröstetem Knoblauch.", Step_EN = "Sprinkle the dish with roasted garlic.", Step_PRT = "Polvilhe o prato com alho assado.", Step_ESP = "Espolvorea el plato con ajo asado." },
                new() { Step_DE = "Reibe die Zitronenschale direkt über dem Gericht.", Step_EN = "Zest the lemon directly over the dish.", Step_PRT = "Rale o limão diretamente sobre o prato.", Step_ESP = "Ralla el limón directamente sobre el plato." },
                new() { Step_DE = "Schneide die Kräuter sehr fein für das Topping.", Step_EN = "Chop the herbs very finely for topping.", Step_PRT = "Pique as ervas bem finas para a cobertura.", Step_ESP = "Pica las hierbas muy finas para la cobertura." },
                new() { Step_DE = "Verteile das Topping gleichmäßig über dem Essen.", Step_EN = "Distribute the topping evenly over the dish.", Step_PRT = "Distribua a cobertura uniformemente sobre o prato.", Step_ESP = "Distribuye la cobertura uniformemente." },
                new() { Step_DE = "Gib einen Spritzer Olivenöl zum Schluss darüber.", Step_EN = "Drizzle a splash of olive oil at the end.", Step_PRT = "Regue com um fio de azeite no final.", Step_ESP = "Añade un chorrito de aceite al final." },
                new() { Step_DE = "Stelle das Gericht kurz beiseite, damit es nachzieht.", Step_EN = "Let the dish sit briefly to meld flavors.", Step_PRT = "Deixe o prato descansar um pouco para harmonizar.", Step_ESP = "Deja reposar el plato un momento." },
                new() { Step_DE = "Serviere das Gericht auf vorgewärmten Tellern.", Step_EN = "Serve the dish on warmed plates.", Step_PRT = "Sirva o prato em pratos aquecidos.", Step_ESP = "Sirve el plato en platos calientes." },
                new() { Step_DE = "Rühre den Teig in kreisenden Bewegungen.", Step_EN = "Stir the batter in circular motions.", Step_PRT = "Misture a massa com movimentos circulares.", Step_ESP = "Remueve la masa en movimientos circulares." },
                new() { Step_DE = "Lasse den Teig 10 Minuten ruhen.", Step_EN = "Let the batter rest for 10 minutes.", Step_PRT = "Deixe a massa descansar por 10 minutos.", Step_ESP = "Deja reposar la masa 10 minutos." },
                new() { Step_DE = "Erhitze eine Pfanne für Pfannkuchen.", Step_EN = "Heat a pan for pancakes.", Step_PRT = "Aqueça uma frigideira para panquecas.", Step_ESP = "Calienta una sartén para panqueques." },
                new() { Step_DE = "Gib eine Kelle Teig in die Pfanne.", Step_EN = "Pour a ladle of batter into the pan.", Step_PRT = "Despeje uma concha de massa na frigideira.", Step_ESP = "Vierte un cucharón de masa en la sartén." },
                new() { Step_DE = "Wende den Pfannkuchen, wenn er Blasen wirft.", Step_EN = "Flip the pancake when bubbles form.", Step_PRT = "Vire a panqueca quando surgirem bolhas.", Step_ESP = "Da la vuelta cuando aparezcan burbujas." },
                new() { Step_DE = "Halte die Pfannkuchen im Ofen warm.", Step_EN = "Keep the pancakes warm in the oven.", Step_PRT = "Mantenha as panquecas aquecidas no forno.", Step_ESP = "Mantén los panqueques calientes en el horno." },
                new() { Step_DE = "Schneide das Gemüse in gleich große Stücke für gleichmäßiges Garen.", Step_EN = "Cut the vegetables into equal pieces for even cooking.", Step_PRT = "Corte os legumes em pedaços iguais para cozinhar por igual.", Step_ESP = "Corta las verduras en piezas iguales para cocción uniforme." },
                new() { Step_DE = "Erhitze den Ofen auf 200 °C.", Step_EN = "Preheat the oven to 200°C.", Step_PRT = "Pré-aqueça o forno a 200 °C.", Step_ESP = "Precalienta el horno a 200 °C." },
                new() { Step_DE = "Lege das Gemüse auf ein Blech und beträufle es.", Step_EN = "Spread the vegetables on a tray and drizzle them.", Step_PRT = "Disponha os legumes em uma assadeira e regue.", Step_ESP = "Coloca las verduras en una bandeja y rocía." },
                new() { Step_DE = "Wende das Gemüse nach der Hälfte der Backzeit.", Step_EN = "Turn the vegetables halfway through roasting.", Step_PRT = "Vire os legumes na metade do tempo.", Step_ESP = "Da la vuelta a las verduras a mitad de cocción." },
                new() { Step_DE = "Röste das Gemüse bis es leicht karamellisiert.", Step_EN = "Roast until the vegetables are lightly caramelized.", Step_PRT = "Asse até os legumes caramelizarem levemente.", Step_ESP = "Asa hasta que las verduras se caramelicen ligeramente." },
                new() { Step_DE = "Schneide die Paprika für das Ofengemüse in Streifen.", Step_EN = "Cut the peppers into strips for roasting.", Step_PRT = "Corte os pimentões em tiras para assar.", Step_ESP = "Corta los pimientos en tiras para asar." },
                new() { Step_DE = "Schneide die Zwiebeln für das Ofengemüse in Spalten.", Step_EN = "Cut the onions into wedges for roasting.", Step_PRT = "Corte as cebolas em gomos para assar.", Step_ESP = "Corta las cebollas en gajos para asar." },
                new() { Step_DE = "Lege das Ofengemüse kurz unter den Grill.", Step_EN = "Broil the roasted vegetables briefly.", Step_PRT = "Gratine os legumes rapidamente.", Step_ESP = "Gratina las verduras brevemente." },
                new() { Step_DE = "Bestreue das Ofengemüse mit Kräutern.", Step_EN = "Sprinkle the roasted vegetables with herbs.", Step_PRT = "Polvilhe os legumes assados com ervas.", Step_ESP = "Espolvorea las verduras asadas con hierbas." },
                new() { Step_DE = "Schneide den Braten in gleichmäßige Scheiben.", Step_EN = "Slice the roast into even slices.", Step_PRT = "Fatie o assado em fatias uniformes.", Step_ESP = "Corta el asado en rebanadas uniformes." },
                new() { Step_DE = "Lege die Bratenscheiben zurück in die Sauce.", Step_EN = "Return the roast slices to the sauce.", Step_PRT = "Devolva as fatias do assado ao molho.", Step_ESP = "Devuelve las rebanadas a la salsa." },
                new() { Step_DE = "Lass die Bratenscheiben kurz in der Sauce ziehen.", Step_EN = "Let the slices sit briefly in the sauce.", Step_PRT = "Deixe as fatias descansarem no molho.", Step_ESP = "Deja que las rebanadas se impregnen en la salsa." },
                new() { Step_DE = "Schneide die Beilage in mundgerechte Stücke.", Step_EN = "Cut the side dish into bite-sized pieces.", Step_PRT = "Corte o acompanhamento em pedaços para comer.", Step_ESP = "Corta la guarnición en trozos de bocado." },
                new() { Step_DE = "Rühre die Sauce noch einmal kräftig durch.", Step_EN = "Stir the sauce vigorously once more.", Step_PRT = "Mexa o molho vigorosamente mais uma vez.", Step_ESP = "Remueve la salsa enérgicamente una vez más." },
                new() { Step_DE = "Schmecke die Sauce mit einem Schuss Essig ab.", Step_EN = "Finish the sauce with a splash of vinegar.", Step_PRT = "Finalize o molho com um toque de vinagre.", Step_ESP = "Termina la salsa con un chorrito de vinagre." },
                new() { Step_DE = "Gib das Fleisch erst kurz vor dem Servieren in die Sauce.", Step_EN = "Add the meat to the sauce shortly before serving.", Step_PRT = "Adicione a carne ao molho pouco antes de servir.", Step_ESP = "Añade la carne a la salsa justo antes de servir." },
                new() { Step_DE = "Stelle das Gericht warm, aber koche es nicht weiter.", Step_EN = "Keep the dish warm without further boiling.", Step_PRT = "Mantenha o prato aquecido sem ferver.", Step_ESP = "Mantén el plato caliente sin hervir." }
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
