using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services
{
    public interface IRecipeSwapStepAiService
    {
        Task<IReadOnlyList<string>> RewriteStepsForSwapsAsync(
            string recipeTitle,
            string language,
            IReadOnlyList<string> originalSteps,
            IReadOnlyList<(string Name, string Quantity, string Unit)> ingredientsAfterSwap,
            IReadOnlyList<IngredientSwap> swaps,
            CancellationToken cancellationToken);
    }

    public sealed class RecipeSwapStepAiService : IRecipeSwapStepAiService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string DefaultOpenAiModel = "gpt-4o-mini";
        private const int AiRequestTimeoutSeconds = 45;

        private readonly HttpClient _httpClient;
        private readonly string _openAiApiKey;
        private readonly string _openAiModel;

        public RecipeSwapStepAiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _openAiApiKey = (config["OpenAI:ApiKey"] ?? config["SecretKeyOpenAi"] ?? string.Empty).Trim();
            _openAiModel = (config["OpenAI:RecipeSwapStepModel"] ?? config["OpenAI:Model"] ?? DefaultOpenAiModel).Trim();
        }

        public async Task<IReadOnlyList<string>> RewriteStepsForSwapsAsync(
            string recipeTitle,
            string language,
            IReadOnlyList<string> originalSteps,
            IReadOnlyList<(string Name, string Quantity, string Unit)> ingredientsAfterSwap,
            IReadOnlyList<IngredientSwap> swaps,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API key is missing.");
            }

            var responseLanguage = GetResponseLanguageName(language);

            var ingredientsLines = ingredientsAfterSwap
                .Select(x => $"- {x.Name}: {x.Quantity} {x.Unit}".Trim())
                .ToList();

            var swapsLines = swaps.Select(s =>
                    $"- {s.FromName} -> {s.ToName} ({s.NewQuantity} {s.Unit})")
                .ToList();

            var stepsLines = originalSteps.Select((s, i) => $"{i + 1}. {s}").ToList();

            var prompt = $@"
You are a recipe editor.

Task:
Rewrite the preparation steps so they correctly match the swapped ingredients.
Do not just do a 1:1 word replacement. Adjust technique, timing, and order if needed.
If the main protein changed (e.g. pork -> chicken), you MUST adapt cooking method, temperature, and cooking time accordingly.
Remove or replace steps that are inappropriate for the new ingredient (e.g. do not tell the user to score chicken like a roast with a fat cap).
If there are multiple similar ingredients (e.g. red lentils AND green lentils), keep them clearly distinct in the steps.
Only apply the swaps listed under ""Swaps applied""; do NOT assume other ingredients in the same category were swapped.
Avoid vague generic references like ""add the lentils"" when more than one lentil/legume remains. Name the specific ingredient(s) as listed.

Output language: {responseLanguage}

IMPORTANT: Use the ingredient names EXACTLY as shown in the ""Ingredient list"" below. These names are already in the correct language ({responseLanguage}). Write the complete steps in {responseLanguage}, using these ingredient names.

Recipe title: {recipeTitle}

Ingredient list (after swap):
{string.Join("\n", ingredientsLines)}

Swaps applied:
{string.Join("\n", swapsLines)}

Original steps:
{string.Join("\n", stepsLines)}

Rules:
- Return ONLY the new steps as JSON (schema enforced).
- Steps must be cookable for beginners (temperatures/times/visual cues where appropriate).
- Do not invent new ingredients that are not in the ingredient list after swap.
- Keep steps concise (usually 6-12 steps).
- Write everything in {responseLanguage}, including using the ingredient names from the list above!";

            var requestBody = new
            {
                model = string.IsNullOrWhiteSpace(_openAiModel) ? DefaultOpenAiModel : _openAiModel,
                input = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = prompt.Trim() }
                        }
                    }
                },
                max_output_tokens = 1400,
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "recipe_swap_step_preview",
                        strict = true,
                        schema = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                steps = new
                                {
                                    type = "array",
                                    minItems = 1,
                                    maxItems = 30,
                                    items = new { type = "string" }
                                }
                            },
                            required = new[] { "steps" }
                        }
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            using var response = await SendWithTimeoutAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseBody));
            }

            using var doc = JsonDocument.Parse(responseBody);
            var outputText = TryExtractOutputText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw new InvalidOperationException("OpenAI returned empty output.");
            }

            SwapStepPreview? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<SwapStepPreview>(outputText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("OpenAI returned invalid JSON.", ex);
            }

            var steps = parsed?.Steps?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList()
                        ?? new List<string>();
            if (steps.Count == 0)
            {
                throw new InvalidOperationException("OpenAI returned no steps.");
            }

            return steps;
        }

        private sealed class SwapStepPreview
        {
            public List<string> Steps { get; set; } = new();
        }

        private async Task<HttpResponseMessage> SendWithTimeoutAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(AiRequestTimeoutSeconds));

            try
            {
                return await _httpClient.SendAsync(request, timeoutCts.Token);
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                throw new InvalidOperationException($"AI request timed out after {AiRequestTimeoutSeconds} seconds.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Network error while calling OpenAI.", ex);
            }
        }

        private static string BuildApiErrorMessage(HttpStatusCode statusCode, string? responseContent)
        {
            var compactBody = (responseContent ?? string.Empty).Trim();
            if (compactBody.Length > 700)
            {
                compactBody = compactBody.Substring(0, 700) + "...";
            }

            return string.IsNullOrWhiteSpace(compactBody)
                ? $"OpenAI API returned {(int)statusCode}."
                : $"OpenAI API returned {(int)statusCode}: {compactBody}";
        }

        private static string? TryExtractOutputText(JsonElement root)
        {
            if (root.TryGetProperty("output_text", out var outputTextElement) && outputTextElement.ValueKind == JsonValueKind.String)
            {
                return ExtractJsonPayloadFromString(outputTextElement.GetString());
            }

            if (root.TryGetProperty("output", out var outputElement) && outputElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outputElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var contentItem in contentElement.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                        {
                            var extracted = ExtractJsonPayloadFromString(textElement.GetString());
                            if (!string.IsNullOrWhiteSpace(extracted))
                            {
                                return extracted;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static string? ExtractJsonPayloadFromString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return trimmed;
            }

            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return null;
        }

        private static string GetResponseLanguageName(string language)
        {
            return (language ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "en" => "English",
                "es" => "Spanish",
                "pt" => "Portuguese",
                "id" => "Indonesian",
                "nl" => "Dutch",
                "sv" => "Swedish",
                "da" => "Danish",
                "nb" => "Norwegian Bokmal",
                "ms" => "Malay",
                _ => "German"
            };
        }
    }
}
