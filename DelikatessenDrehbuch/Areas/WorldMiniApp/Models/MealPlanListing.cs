namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class MealPlanListing
    {
        public int Id { get; set; }
        public string SellerHash { get; set; }
        public string SellerName { get; set; }
        public int MealPlanId { get; set; }
        public WorldUserMealPlan MealPlan { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DayCount { get; set; }
        public int RecipeCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public int SoldCount { get; set; }
    }
}
