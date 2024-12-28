namespace DelikatessenDrehbuch.Models
{
   
    public class ShowRecipesModel
    {
        public List<Recipes> RecipesList { get; set; } = new();
        public int RecipesToShow { get; set; }  
    }
}
