namespace DelikatessenDrehbuch.Models
{
    public class FullRecipes
    {
        public Recipes Recipes { get; set; }
        public virtual List<IngredientHandlerModel> IngredientHandler { get; set; }
      

        public FullRecipes()
        {
            Recipes = new Recipes();
            IngredientHandler = new List<IngredientHandlerModel>();
        }
    }
}
