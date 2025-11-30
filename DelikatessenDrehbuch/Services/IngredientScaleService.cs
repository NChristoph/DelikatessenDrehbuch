using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class IngredientScaleService:IIngredientScaleService
    {
        private readonly IIngredientService _ingredientService;
        private readonly IRecipesService _recipesService;
        private readonly ApplicationDbContext _context;

        public IngredientScaleService(IIngredientService ingredientService,IRecipesService recipesService,
                                      ApplicationDbContext context)
        {
            _ingredientService = ingredientService;
            _recipesService = recipesService;
            _context = context;
        }

        private int GetRecipePersonCount(int recipeId)
        {
            return (int)_context.Recipes.FirstOrDefault(x => x.Id == recipeId).RecipePersonCount;
        }

        private List<IngredientHandlerModel> GetIngredientHandlerModelsAsync(int recipeId)
        {
            
            return  _ingredientService.GetIngredientHandlerByRecipesId(recipeId);
            
        }

        private List<IngredientHandlerModel> ScaleIngredientQuantity(List<IngredientHandlerModel> ingredients,
                                                                    int currentPersonCount,
                                                                    int targetPersonCount)
        {
            var ingredientHandlers = new List<IngredientHandlerModel>();
            foreach (var ing in ingredients)
            {
                IngredientHandlerModel model = new()
                {
                    Ingredient = ing.Ingredient,
                    Measure = ing.Measure,
                    Quantity = new Quantity()
                    {
                        Id = ing.Quantity.Id,
                        Quantitys = Math.Round((((ing.Quantity.Quantitys) / currentPersonCount) * targetPersonCount * 2), MidpointRounding.AwayFromZero) / 2
                    }
                };
                ingredientHandlers.Add(model);
            }
            return ingredientHandlers;
        }

        public (IEnumerable<IGrouping<string, IngredientHandlerModel>>, bool, bool)
            GetScaledIngredienthandler(int recipeId,int targetPersonCount)
        {
            int currentPersonCount = GetRecipePersonCount(recipeId);
            var currentIngredienHandler =  GetIngredientHandlerModelsAsync(recipeId);
            var scaledHandlers = ScaleIngredientQuantity(currentIngredienHandler,
                                                         currentPersonCount,
                                                         targetPersonCount);

            var ingredients = scaledHandlers.Select(x => x.Ingredient.Name.ToLower()).ToList();

            bool salt = ingredients.Contains("salz");
            bool pepper = ingredients.Contains("pfeffer");

            if (salt)
                scaledHandlers.Remove(scaledHandlers.Find(x => x.Ingredient.Name.Trim().ToLower() == "salz"));
            if (pepper)
                scaledHandlers.Remove(scaledHandlers.Find(x => x.Ingredient.Name.Trim().ToLower() == "pfeffer"));



            return (CombineIngredienthanderModel(scaledHandlers),salt,pepper);

            
        }

        public IEnumerable<IGrouping<string, IngredientHandlerModel>> CombineIngredienthanderModel(List<IngredientHandlerModel> listToSort)
        {

            var combined = listToSort.GroupBy(ih => new { ih.Ingredient.Id, Unit = ih.Measure.UnitOfMeasurement })
                                     .Select(g =>
                                     {

                                         var first = g.First();
                                         var unit = first.Measure.UnitOfMeasurement;

                                         bool isG = unit.Equals("g.", StringComparison.OrdinalIgnoreCase);
                                         bool isMl = unit.Equals("ml", StringComparison.OrdinalIgnoreCase);
                                         bool isB = unit.Equals("Blatt", StringComparison.OrdinalIgnoreCase);
                                         bool isTL = unit.Equals("TL.", StringComparison.OrdinalIgnoreCase);
                                         bool isEL = unit.Equals("EL.", StringComparison.OrdinalIgnoreCase);
                                         bool isPr = unit.Equals("Prise", StringComparison.OrdinalIgnoreCase);
                                         bool isStk = unit.Equals("Stk.", StringComparison.OrdinalIgnoreCase);

                                         var avg = first.Ingredient?.AverageWeight ?? 0d; // ggf. AverageWeightGrams
                                         double sum = 0.0;
                                         string outUnit = "";




                                         if (g.First().Ingredient.Group.Name == "Gemüse" ||
                                             g.First().Ingredient.Group.Name == "Obst")
                                         {

                                             bool check = (isG && avg != 0d && g.First().Ingredient.GrammOnly == null);
                                             sum = check
                                             ? g.Sum(x => x.Quantity.Quantitys) / avg
                                             : g.Sum(x => x.Quantity.Quantitys);

                                             outUnit = check ? $"Stk." : unit;

                                         }
                                         else if (g.First().Ingredient.Group.Name == "Gewürze")
                                         {
                                             var name = g.First().Ingredient.Name;
                                             if (!isPr && name != "Salz" && name != "Pfeffer")
                                             {
                                                 bool check = (isEL);
                                                 sum = check
                                                 ? g.Sum(x => x.Quantity.Quantitys) * 2
                                                 : g.Sum(x => x.Quantity.Quantitys);
                                                 outUnit = check ? "TL." : unit;
                                             }
                                             else
                                             {
                                                 sum = 0;
                                                 outUnit = "Etwas";
                                             }



                                         }
                                         else if (g.First().Ingredient.Group.Name == "Grundnahrungsmittel")
                                         {
                                             bool check = (isEL || isTL || isPr && avg > 0d);
                                             sum = check
                                             ? g.Sum(x => x.Quantity.Quantitys) * avg
                                             : g.Sum(x => x.Quantity.Quantitys);
                                             outUnit = check ? "g." : unit;
                                         }
                                         else if (g.First().Ingredient.Group.Name == "Milchprodukte")
                                         {

                                             bool check = (isEL || isTL && avg > 0d);
                                             sum = check
                                             ? g.Sum(x => x.Quantity.Quantitys) * avg
                                             : g.Sum(x => x.Quantity.Quantitys);
                                             outUnit = check ? "g." : unit;
                                         }

                                         else
                                         {
                                             bool check = (isEL && avg > 0d);


                                             sum = check
                                                 ? g.Sum(x => x.Quantity.Quantitys) * avg
                                                 : g.Sum(x => x.Quantity.Quantitys);
                                             outUnit = check ? "ml" : unit;
                                         }




                                         return new IngredientHandlerModel
                                         {

                                             Ingredient = first.Ingredient,
                                             Measure = new Measure { UnitOfMeasurement = outUnit },
                                             Quantity = new Quantity { Quantitys = sum }
                                         };
                                     })
                                     .GroupBy(ih => ih.Ingredient.Group.Name);
                                     

            return combined;
        }
    }
}
