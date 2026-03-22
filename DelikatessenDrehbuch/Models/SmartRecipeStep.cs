namespace DelikatessenDrehbuch.Models
{
    public class SmartRecipeStep
    {
        public int Id { get; set; }
        public string MasterStepKey { get; set; } = string.Empty;
        public string VariablesJson { get; set; } = "{}";
        public int? Phase { get; set; }
        public string? Equipment { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public virtual ICollection<RecipeJoinSmartStep> RecipeLinks { get; set; } = new List<RecipeJoinSmartStep>();
    }
}
