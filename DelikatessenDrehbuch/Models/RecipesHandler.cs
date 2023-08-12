namespace DelikatessenDrehbuch.Models
{
    public class RecipesHandler
    {
        public int Id { get; set; }
        public Recipes Recipe { get; set; }
        public IngredientHandler? IngredientHandler { get; set; }
    }
}
