namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldSharedShoppingList
    {
        public int Id { get; set; }
        public string ShareToken { get; set; }
        public string? UserHash { get; set; }
        public string ItemsJson { get; set; }
        public string CheckedJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
