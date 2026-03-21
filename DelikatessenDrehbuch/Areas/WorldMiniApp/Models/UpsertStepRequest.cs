namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class UpsertStepRequest
    {
        public string De { get; set; } = string.Empty;
        public string En { get; set; } = string.Empty;
        public string Esp { get; set; } = string.Empty;
        public string Prt { get; set; } = string.Empty;
        public int Phase { get; set; }
        public int Equipment { get; set; }
    }
}
