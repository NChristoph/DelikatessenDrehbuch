using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class MyAreaViewModel
    {
        public string? UserHash { get; set; }
        public List<WorldUserMealPlan> CreatedMealPlans { get; set; } = new();
        public List<MealPlanPurchase> Purchases { get; set; } = new();
    }
}
