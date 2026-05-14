namespace DelikatessenDrehbuch.Models
{
    public class EditRecipesModel
    {
        public Recipes Recipes { get; set; }
        public List<IngredientHandlerModel> IngredientHandler { get; set; }
        public List<Measure> Measure { get; set; }
        public string Querys { get; set; }

        public RecipeBaseData RecipeBaseData { get; set; }= new RecipeBaseData();

        public List<IngredientsAndNutrients> IngredientsAndNutrients { get; set; }
        public List<IngredientMeasureQuantity> IngredientMeasureQuantity { get; set; }

        public List<RecipeSteps> RecipeSteps { get; set; }  // NEW: Normalized Translation System
        public EditRecipesModel()
        {
            Recipes = new Recipes();
            IngredientHandler = new List<IngredientHandlerModel>();
            Measure = new List<Measure>();
            IngredientsAndNutrients= new List<IngredientsAndNutrients>();

        }
    }
}
