namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class MiniAppSetupModel
    {
        // Für die Radio-Buttons (Alles, Veggie, Vegan)
        public string DietType { get; set; } = "Alles";
      

        public List<string> Filters { get; set; } = new List<string>();

        public int DayCount { get; set; } = 7;
        public int PersonCount { get; set; } = 2;
    }
}
