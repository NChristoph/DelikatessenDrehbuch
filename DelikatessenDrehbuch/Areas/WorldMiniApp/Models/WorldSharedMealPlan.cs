using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldSharedMealPlan
    {
        public int Id { get; set; }
        public string ShareToken { get; set; }
        public string? UserHash { get; set; }
        public string Title { get; set; }
        public int PersonCount { get; set; }
        public string MealPlanJson { get; set; }
        public string ShoppingListJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ShoppingListItem
    {
        public string GroupName { get; set; }
        public string IngredientName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
    }
}
