namespace DelikatessenDrehbuch.Models
{
    public sealed class IngredientResolutionCandidate
    {
        public int IngredientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryKey { get; set; } = string.Empty;
        public string CategoryLabel { get; set; } = string.Empty;
        public decimal ProteinPer100g { get; set; }
        public decimal CarbsPer100g { get; set; }
        public decimal FatPer100g { get; set; }
        public decimal FiberPer100g { get; set; }
        public decimal CaloriesPer100g { get; set; }
        public decimal Score { get; set; }
        public string MatchType { get; set; } = string.Empty;
    }
}
