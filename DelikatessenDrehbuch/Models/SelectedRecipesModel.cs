namespace DelikatessenDrehbuch.Models
{
    public class SelectedRecipesModel
    {
        public FullRecipeData FullRecipeData { get; set; }
        public List<string> Querys { get; set; }
        public List<Recipes> RecipeSuggestions { get; set; }

        public List<NutrienHandler> NutrienHandlers { get; set; }
    }
}
