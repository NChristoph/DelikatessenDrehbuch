namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class SharedMealPlanRequest
    {
        public string MealPlanJson { get; set; }
        public int PersonCount { get; set; }
        public string Title { get; set; }
        public string? UserHash { get; set; }
        public int? SharedFeedId { get; set; }
    }
}
