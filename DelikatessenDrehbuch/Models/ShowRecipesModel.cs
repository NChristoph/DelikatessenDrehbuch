namespace DelikatessenDrehbuch.Models
{

    public class ShowRecipesModel
    {
        public List<Recipes> RecipesList { get; set; } = new();
        public bool Searchrecipes { get; set; }
    }
}
