using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class SavedMealPlans
    {
        public int Id { get; set; }
        public string UserMail { get; set; }
        public string MealPlanJson { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.Now;
        public int PersonCount { get; set; }
        public string? ShoppingList { get; set; } 
        public string? Token { get; set; }
        [NotMapped]
        public Dictionary<int, List<int>> MealPlanDictionary { get; set; }
        
    }
}
