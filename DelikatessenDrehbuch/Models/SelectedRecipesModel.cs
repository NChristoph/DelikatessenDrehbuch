namespace DelikatessenDrehbuch.Models
{
    public class SelectedRecipesModel
    {
        public FullRecipes FullRecipes { get; set; }
        public List<Recipes> RecipeSuggestions { get; set; }

        public List<NutrienHandler> NutrienHandlers { get; set; }
    }
}
