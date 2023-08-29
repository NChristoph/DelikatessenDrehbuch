namespace DelikatessenDrehbuch.Models
{
    public class RecipesHandler
    {
        public int Id { get; set; }
        public Recipes Recipe { get; set; }
        public IngredientHandlerModel? IngredientHandler { get; set; }
    }
}
