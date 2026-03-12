namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldAdPreferenceInterest
    {
        public int Id { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public string InterestType { get; set; } = string.Empty;
        public string InterestKey { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public int SignalCount { get; set; }
        public DateTime LastSignalAtUtc { get; set; }
    }
}
