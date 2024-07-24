namespace DelikatessenDrehbuch.Models
{
    public class UserPreferencesRecipe
    {
        public int Id { get; set; }
        public string UserEmail { get; set; }
        public Recipes Recipes { get; set; }

    }
}
