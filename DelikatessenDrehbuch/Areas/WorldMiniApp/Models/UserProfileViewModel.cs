using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class UserProfileViewModel
    {
        public WorldAppUser User { get; set; }

        // Immer sichtbar
        public List<WorldUserPosting> LikedRecipes { get; set; }
        public List<WorldAppUser> Following { get; set; }
        public List<WorldUserMealPlan> MealPlans { get; set; } = new();
        public List<WorldUserMealPlan> CreatedMealPlans { get; set; } = new();
        public List<WorldUserMealPlan> PurchasedMealPlans { get; set; } = new();
        public List<MealPlanPurchase> Purchases { get; set; } = new();

        // Nur für "orb" User sichtbar
        public List<WorldUserPosting> MyVideos { get; set; } = new List<WorldUserPosting>();
        public int FollowerCount { get; set; } // Wer folgt mir?
    }
}


