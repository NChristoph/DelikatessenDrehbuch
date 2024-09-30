namespace DelikatessenDrehbuch.Models
{
    public class WeeklyRecipeHandler
    {
        public int Id {  get; set; }
        public Recipes? Breakfast { get; set; }
        public Recipes? Lunch { get; set; }
        public Recipes? Dinner { get; set; }
        public WeeklyRecipe WeeklyRecipe { get; set; }
    }
}
 