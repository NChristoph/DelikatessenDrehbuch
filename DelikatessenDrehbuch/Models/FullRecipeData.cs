namespace DelikatessenDrehbuch.Models
{
    public class FullRecipeData
    {
        public int Id { get; set; }
        public Recipes Recipes { get; set; }
        public virtual List<IngredientHandlerModel> IngredientHandler { get; set; }
        public virtual List<Like>? Likes { get; set; }
        public virtual List<Recession>? Recession { get; set; }
        public virtual List<Measure> Measure { get; set; }
        public virtual List<NutrienHandler> NutrientHandler { get; set; }
        public virtual List<Recipes> RecipeSuggestions { get; set; }
        public virtual List<string> Querys { get; set; }

        public FullRecipeData()
        {
            Recipes = new Recipes();
            IngredientHandler = new List<IngredientHandlerModel>();
            Likes = new List<Like>();
            Recession = new List<Recession>();
            Measure = new List<Measure>();
            NutrientHandler = new List<NutrienHandler>();
            RecipeSuggestions=new List<Recipes>();
            Querys = new List<string>();



        }
    }
}
