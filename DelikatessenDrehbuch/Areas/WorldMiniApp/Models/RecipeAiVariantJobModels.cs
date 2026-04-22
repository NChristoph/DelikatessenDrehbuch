using System.Text.Json.Serialization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public static class RecipeAiVariantJobState
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Ready = "ready";
        public const string Error = "error";
        public const string Cancelled = "cancelled";
    }

    public sealed class RecipeAiVariantJobStartResponse
    {
        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = string.Empty;
    }

    public sealed class RecipeAiVariantJobStatusResponse
    {
        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = RecipeAiVariantJobState.Queued;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("recipeId")]
        public int RecipeId { get; set; }

        [JsonPropertyName("variantType")]
        public string VariantType { get; set; } = string.Empty;

        [JsonPropertyName("aiProvider")]
        public string AiProvider { get; set; } = "openai";

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    internal sealed class RecipeAiVariantJobEnvelope
    {
        public string JobId { get; set; } = string.Empty;
        public int RecipeId { get; set; }
        public string VariantType { get; set; } = string.Empty;
        public string AiProvider { get; set; } = "openai";
        public string Language { get; set; } = "de";
        public string? UserHash { get; set; }
        public RecipeAiTransformRequest Request { get; set; } = new();
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    internal sealed class RecipeAiVariantJobCacheItem
    {
        public string JobId { get; set; } = string.Empty;
        public int RecipeId { get; set; }
        public string VariantType { get; set; } = string.Empty;
        public string AiProvider { get; set; } = "openai";
        public string Language { get; set; } = "de";
        public string State { get; set; } = RecipeAiVariantJobState.Queued;
        public string? Message { get; set; }
        public string? Title { get; set; }
        public string? ResultJson { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

