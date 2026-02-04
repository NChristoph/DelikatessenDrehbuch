using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldMealPlanViewModel
    {
        public string Title { get; set; } = "Mein Plan";
        public int PersonCount { get; set; } = 1;
        public List<MealPlanerModel> MealPlan { get; set; } = new();
        public List<ShoppingListItem> ShoppingList { get; set; } = new();
    }
}
