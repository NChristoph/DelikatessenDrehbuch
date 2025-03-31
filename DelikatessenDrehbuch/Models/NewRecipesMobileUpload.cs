namespace DelikatessenDrehbuch.Models
{
    public class NewRecipesMobileUpload
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? MealPlan { get; set; }
        public string Category { get; set; }
        public string Calories { get; set; }
        public string PreperationTime { get; set; }
        public string Preperation { get; set; }
        public string Ingredients { get; set; }
        public string Querys { get; set; }
        public string Description { get; set; }

        public IFormFile RecipesImage { get; set; }


    }
}
