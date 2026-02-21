namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserMealPlan
    {
        public int Id { get; set; }
        public string UserHash { get; set; }
        public string? Settings { get; set; }
        public DateTime CreationTime { get; set; }=DateTime.Now;
        public string? MealPlan { get; set; }

        public string? Title { get; set; }
    }
}
