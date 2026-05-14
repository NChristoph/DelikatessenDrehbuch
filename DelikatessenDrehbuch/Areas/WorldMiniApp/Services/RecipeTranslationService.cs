using System.Text.Json;
using System.Text.Json.Serialization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    /// <summary>
    /// NEUER simpler Service: Übersetzt ganzes Rezept in alle 10 Sprachen
    /// Ersetzt das komplexe Master-Step-System
    /// </summary>
    public class RecipeTranslationService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";
        private const string DefaultModel = "gpt-4o-mini";
        private const int TimeoutSeconds = 60;

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<RecipeTranslationService> _logger;

        public RecipeTranslationService(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<RecipeTranslationService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        /// <summary>
        /// Übersetzt Rezept-Titel und Steps in alle 10 Sprachen
        /// (Ingredients sind schon mehrsprachig in der DB!)
        /// </summary>
        public async Task<RecipeTranslationResult> TranslateRecipeAsync(
            string title,
            string steps,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("🌍 Translating recipe: {Title}", title);

            try
            {
                var apiKey = _config["OpenAI:ApiKey"] ?? _config["SecretKeyOpenAi"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("OpenAI API key is missing");
                }

                var prompt = BuildTranslationPrompt(title, steps);

                var requestBody = new
                {
                    model = DefaultModel,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional recipe translator. Provide accurate, natural translations while maintaining cooking terminology and imperative verb forms." },
                        new { role = "user", content = prompt }
                    },
                    response_format = new { type = "json_object" },
                    temperature = 0.3, // Lower for consistent translations
                    max_tokens = 3000
                };

                var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("✅ OpenAI Response - Status: {Status}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ OpenAI API error: {Status} - {Body}", response.StatusCode, responseBody);
                    throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
                }

                var aiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseBody);
                var content = aiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("OpenAI returned empty response");
                }

                var result = JsonSerializer.Deserialize<TranslationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return new RecipeTranslationResult
                {
                    Success = true,
                    Title = result?.Title ?? new TranslationSet(),
                    Steps = result?.Steps ?? new TranslationSet()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate recipe: {Title}", title);
                return new RecipeTranslationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string BuildTranslationPrompt(string title, string steps)
        {
            return $@"Translate this cooking recipe into 10 languages: German (de), English (en), Spanish (esp), Portuguese (prt), Indonesian (id), Dutch (nl), Swedish (sv), Danish (da), Norwegian (no), and Malay (ms).

**Recipe:**

**Title:** {title}

**Cooking Steps:**
{steps}

**Guidelines:**
- Use IMPERATIVE verb forms in all languages (e.g., German: ""Schneide"", English: ""Cut"", not infinitives)
- Maintain proper grammar and natural phrasing for each language
- Keep cooking terminology accurate
- Preserve formatting (line breaks, numbering if present)
- Be context-aware (consider the dish type for better translations)

**Output Format (JSON):**
{{
  ""title"": {{
    ""de"": ""German title"",
    ""en"": ""English title"",
    ""esp"": ""Spanish title"",
    ""prt"": ""Portuguese title"",
    ""id"": ""Indonesian title"",
    ""nl"": ""Dutch title"",
    ""sv"": ""Swedish title"",
    ""da"": ""Danish title"",
    ""no"": ""Norwegian title"",
    ""ms"": ""Malay title""
  }},
  ""steps"": {{
    ""de"": ""German steps"",
    ""en"": ""English steps"",
    ""esp"": ""Spanish steps"",
    ""prt"": ""Portuguese steps"",
    ""id"": ""Indonesian steps"",
    ""nl"": ""Dutch steps"",
    ""sv"": ""Swedish steps"",
    ""da"": ""Danish steps"",
    ""no"": ""Norwegian steps"",
    ""ms"": ""Malay steps""
  }}
}}";
        }

        // DTOs

        private class OpenAiResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message? Message { get; set; }
        }

        private class Message
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }

        private class TranslationResponse
        {
            [JsonPropertyName("title")]
            public TranslationSet? Title { get; set; }

            [JsonPropertyName("steps")]
            public TranslationSet? Steps { get; set; }
        }
    }

    public class RecipeTranslationResult
    {
        public bool Success { get; set; }
        public TranslationSet Title { get; set; } = new();
        public TranslationSet Steps { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class TranslationSet
    {
        [JsonPropertyName("de")]
        public string De { get; set; } = "";

        [JsonPropertyName("en")]
        public string En { get; set; } = "";

        [JsonPropertyName("esp")]
        public string Esp { get; set; } = "";

        [JsonPropertyName("prt")]
        public string Prt { get; set; } = "";

        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("nl")]
        public string Nl { get; set; } = "";

        [JsonPropertyName("sv")]
        public string Sv { get; set; } = "";

        [JsonPropertyName("da")]
        public string Da { get; set; } = "";

        [JsonPropertyName("no")]
        public string No { get; set; } = "";

        [JsonPropertyName("ms")]
        public string Ms { get; set; } = "";
    }
}
