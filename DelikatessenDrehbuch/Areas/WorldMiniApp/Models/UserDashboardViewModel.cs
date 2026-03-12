namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class UserDashboardViewModel
    {
        public bool IsLoggedInWithWorldMiniApp { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public int SoldMealPlanCount { get; set; }
        public decimal TotalCreatorRevenue { get; set; }
        public decimal OpenWldAmount { get; set; }
        public decimal OpenUsdcAmount { get; set; }
        public CreatorWatchAnalyticsViewModel WatchAnalytics { get; set; } = new();
        public AdPreferenceDashboardViewModel AdPreferences { get; set; } = new();
        public List<MealPlanPurchase> SalesHistory { get; set; } = new();
        public List<WildCoinTransaction> WildCoinHistory { get; set; } = new();
    }
}
