namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiTransformRequest
    {
        public int RecipeId { get; set; }
        public string VariantType { get; set; } = string.Empty;
        public string? UserNote { get; set; }
        public int AppliedChangeCount { get; set; }
    }
}
