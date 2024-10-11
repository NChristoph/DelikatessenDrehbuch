using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DelikatessenDrehbuch.Models
{
    public class AdminControllerModel
    {
        public MealPlan WeeklyRecipe { get; set; }
        public MealPlanHandler WeeklyRecipeHandler { get; set; }
        public List<MealPlanHandler> WeeklyRecipeHandlers { get; set; }

        public List<SupportMessage> SupportMessage { get; set; }

        public int UserCount { get; set; }
        public int PremiumUser { get; set; }
        public List<Recipes> Recipes { get; set; }

        public AdminControllerModel()
        {
            WeeklyRecipeHandlers = new List<MealPlanHandler>();
            SupportMessage = new List<SupportMessage>();
            Recipes = new List<Recipes>();
        }
    }
}
