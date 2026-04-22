using System.ComponentModel.DataAnnotations;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiVariantJob
    {
        public int Id { get; set; }

        [MaxLength(64)]
        public string JobId { get; set; } = string.Empty;

        public int BaseRecipeId { get; set; }
        public RecipeBaseData BaseRecipe { get; set; } = null!;

        [MaxLength(64)]
        public string VariantType { get; set; } = string.Empty;

        [MaxLength(12)]
        public string Language { get; set; } = "de";

        [MaxLength(32)]
        public string AiProvider { get; set; } = "openai";

        [MaxLength(256)]
        public string? CreatedByUserHash { get; set; }

        [MaxLength(32)]
        public string State { get; set; } = RecipeAiVariantJobState.Queued;

        [MaxLength(400)]
        public string? Message { get; set; }

        [MaxLength(256)]
        public string? Title { get; set; }

        public string RequestJson { get; set; } = "{}";
        public string? ResultJson { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
