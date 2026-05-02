using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services
{
    public interface IRecipeVariantSummaryAiService
    {
        Task<string?> GenerateSummaryAsync(RecipeBaseData recipe, IReadOnlyList<IngredientSwap> swaps, string language, CancellationToken cancellationToken);
        Task<(string title, string? summary)?> GenerateTitleAndSummaryAsync(RecipeBaseData recipe, IReadOnlyList<IngredientSwap> swaps, string language, CancellationToken cancellationToken);
    }

    public sealed class RecipeVariantSummaryAiService : IRecipeVariantSummaryAiService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string DefaultModel = "gpt-4o-mini";

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ILogger<RecipeVariantSummaryAiService> _logger;

        public RecipeVariantSummaryAiService(HttpClient httpClient, IConfiguration config, ILogger<RecipeVariantSummaryAiService> logger)
        {
            _httpClient = httpClient;
            _apiKey =
                (config["OpenAI:ApiKey"] ??
                 config["SecretKeyOpenAi"] ??
                 config["OpenAi:ApiKey"] ??
                 config["OpenAiApiKey"] ??
                 Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                 Environment.GetEnvironmentVariable("OpenAI:ApiKey") ??
                 string.Empty).Trim();
            _model = (config["OpenAI:VariantSummaryModel"] ?? config["OpenAI:Model"] ?? DefaultModel).Trim();
            _logger = logger;
        }

        public async Task<string?> GenerateSummaryAsync(RecipeBaseData recipe, IReadOnlyList<IngredientSwap> swaps, string language, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return null;
            }

            if (recipe == null || swaps == null || swaps.Count == 0)
            {
                return null;
            }

            try
            {
                var responseLanguage = language switch
                {
                    "en" => "English",
                    "es" => "Spanish",
                    "pt" => "Portuguese",
                    _ => "German"
                };

                var swapsText = string.Join("\n", swaps
                    .Take(12)
                    .Select(s => $"- {s.FromName} -> {s.ToName}"));

                var input = $@"You write a short one-sentence summary describing how a recipe variant differs from the original.
Recipe title: {recipe.Title}
Swaps:
{swapsText}

Rules:
- One sentence only.
- Max 160 characters.
- Mention the most important change(s), not every detail.
- Language: {responseLanguage}.";

                var body = new
                {
                    model = _model,
                    input,
                    max_output_tokens = 120,
                    text = new { verbosity = "low" }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using var resp = await _httpClient.SendAsync(req, cancellationToken);
                var respBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Variant summary AI failed: {Status} {Body}", (int)resp.StatusCode, respBody);
                    return null;
                }

                using var doc = JsonDocument.Parse(respBody);
                var outText = TryExtractResponseOutputText(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(outText))
                {
                    return outText;
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Variant summary AI exception");
                return null;
            }
        }

        public async Task<(string title, string? summary)?> GenerateTitleAndSummaryAsync(
            RecipeBaseData recipe,
            IReadOnlyList<IngredientSwap> swaps,
            string language,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return null;
            }

            if (recipe == null || swaps == null || swaps.Count == 0)
            {
                return null;
            }

            try
            {
                var responseLanguage = language switch
                {
                    "en" => "English",
                    "es" => "Spanish",
                    "pt" => "Portuguese",
                    _ => "German"
                };

                var swapsText = string.Join("\n", swaps
                    .Take(12)
                    .Select(s => $"- {s.FromName} -> {s.ToName}"));

                var input = $@"You write a short title and a short description for a recipe variant.
Original recipe title: {recipe.Title}
Swaps applied:
{swapsText}

Rules:
- Language: {responseLanguage}.
- Title: 45-80 characters, natural recipe title, should reflect the new main ingredient(s) if changed.
- Title must NOT mention an ingredient that was replaced by a swap.
- If a swap changes the main protein (e.g., pork->chicken), update the dish name accordingly.
- Summary: 1-2 sentences, max 240 characters. Explain what the dish is AND what's different vs original (not every detail).
- No emojis.
- Return JSON only.";

                var body = new
                {
                    model = _model,
                    input = new object[]
                    {
                        new
                        {
                            role = "system",
                            content = new object[]
                            {
                                new { type = "input_text", text = "Return only valid JSON matching the schema." }
                            }
                        },
                        new
                        {
                            role = "user",
                            content = new object[] { new { type = "input_text", text = input } }
                        }
                    },
                    max_output_tokens = 220,
                    text = new
                    {
                        format = new
                        {
                            type = "json_schema",
                            name = "variant_metadata",
                            strict = true,
                            schema = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    title = new { type = "string" },
                                    summary = new { type = new object[] { "string", "null" } }
                                },
                                required = new[] { "title", "summary" }
                            }
                        }
                    }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using var resp = await _httpClient.SendAsync(req, cancellationToken);
                var respBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Variant metadata AI failed: {Status} {Body}", (int)resp.StatusCode, respBody);
                    return null;
                }

                using var doc = JsonDocument.Parse(respBody);
                var outText = TryExtractResponseOutputText(doc.RootElement);
                if (string.IsNullOrWhiteSpace(outText))
                {
                    _logger.LogWarning("Variant metadata AI succeeded but response had no output text. Body: {Body}", respBody);
                    return null;
                }

                var json = outText.Trim();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                using var parsed = JsonDocument.Parse(json);
                var root = parsed.RootElement;
                var title = (root.GetProperty("title").GetString() ?? string.Empty).Trim();
                var summary = root.GetProperty("summary").ValueKind == JsonValueKind.Null
                    ? null
                    : (root.GetProperty("summary").GetString() ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(title))
                {
                    return null;
                }

                return (title, string.IsNullOrWhiteSpace(summary) ? null : summary);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Variant metadata AI exception");
                return null;
            }
        }

        private static string? TryExtractResponseOutputText(JsonElement root)
        {
            // The Responses API may return either:
            // - output_text: "..."
            // - output: [{ content: [{ type: "output_text", text: "..." }, ...] }, ...]
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (root.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.String)
            {
                return (ot.GetString() ?? string.Empty).Trim();
            }

            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in output.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;

                foreach (var c in content.EnumerateArray())
                {
                    if (c.ValueKind != JsonValueKind.Object) continue;

                    // Common case: { type: "output_text", text: "..." }
                    if (c.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        var s = (text.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }

                    // Some SDKs may use "output_text" property per content item.
                    if (c.TryGetProperty("output_text", out var text2) && text2.ValueKind == JsonValueKind.String)
                    {
                        var s = (text2.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
            }

            return null;
        }
    }
}
