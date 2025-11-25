using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.MealPlaner.Models
{
    public class CreatedMealPlanModel
    {
        public int DayIndex { get; set; }
        public int PersonCount { get; set; }
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public int RecipeId { get; set; }

    }
}
