namespace DelikatessenDrehbuch.Models
{
    public class EditRecipesModel
    {
        public Recipes Recipes { get; set; }
        public List<IngredientHandlerModel> IngredientHandler { get; set; }
        public List<Measure> Measure { get; set; }
        public List<string> Querys { get; set; }
        public EditRecipesModel()
        {
            Recipes = new Recipes();
            IngredientHandler = new List<IngredientHandlerModel>();
            Measure = new List<Measure>();
        }
    }
}
