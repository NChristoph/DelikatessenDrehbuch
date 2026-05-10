using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class RecipeStepTranslationService : IRecipeStepTranslationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<RecipeStepTranslationService> _logger;

        // 4 Hauptsprachen (sofort übersetzt)
        private static readonly string[] CoreLanguages = { "de", "en", "esp", "prt" };

        // 6 On-Demand Sprachen
        private static readonly string[] OnDemandLanguages = { "id", "nl", "sv", "da", "no", "ms" };

        // Sprach-Mapping
        private static readonly Dictionary<string, string> LanguageNames = new()
        {
            { "de", "Deutsch" },
            { "en", "English" },
            { "esp", "Español" },
            { "prt", "Português" },
            { "id", "Bahasa Indonesia" },
            { "nl", "Nederlands" },
            { "sv", "Svenska" },
            { "da", "Dansk" },
            { "no", "Norsk" },
            { "ms", "Bahasa Melayu" }
        };

        public RecipeStepTranslationService(
            ApplicationDbContext db,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IBackgroundTaskQueue queue,
            ILogger<RecipeStepTranslationService> logger)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _queue = queue;
            _logger = logger;
        }

        public Task QueueTranslationAsync(int recipeId)
        {
            return _queue.QueueAsync(async cancellationToken =>
            {
                await TranslateRecipeStepsAsync(recipeId, cancellationToken);
            }).AsTask();
        }

        public async Task TranslateRecipeStepsAsync(int recipeId, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. Lade Recipe mit Steps
                var steps = await _db.RecipeSteps
                    .Where(s => s.RecipeId == recipeId)
                    .OrderBy(s => s.StepOrder)
                    .ToListAsync(cancellationToken);

                if (steps.Count == 0)
                {
                    _logger.LogWarning("Recipe {RecipeId} has no steps", recipeId);
                    return;
                }

                var sourceLang = steps.First().SourceLanguage ?? "de";

                _logger.LogInformation("🌍 Translating recipe {RecipeId} from {SourceLang} to 4 core languages",
                    recipeId, sourceLang);

                // 2. Übersetze in alle 4 Hauptsprachen (minus Source)
                var targetLanguages = CoreLanguages.Where(lang => lang != sourceLang).ToArray();

                foreach (var targetLang in targetLanguages)
                {
                    try
                    {
                        _logger.LogInformation("  → Translating to {Lang}...", targetLang.ToUpper());

                        // Batch-Übersetzung mit GPT
                        var translations = await TranslateBatchWithGPTAsync(
                            steps,
                            sourceLang,
                            targetLang,
                            useGPT4oMini: false, // Hauptsprachen mit GPT-4o
                            cancellationToken);

                        if (translations == null || translations.Count == 0)
                        {
                            _logger.LogWarning("  ⚠️  Translation returned empty for {Lang}", targetLang);
                            continue;
                        }

                        // Speichere in RecipeStepTranslation Tabelle
                        foreach (var (step, translatedText) in translations)
                        {
                            // Prüfe ob schon vorhanden
                            var existing = await _db.RecipeStepTranslations
                                .FirstOrDefaultAsync(t => t.StepId == step.Id && t.Language == targetLang, cancellationToken);

                            if (existing != null)
                            {
                                // Update
                                existing.TranslatedText = translatedText;
                                existing.TranslatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                // Insert
                                var translation = new RecipeStepTranslation
                                {
                                    RecipeId = recipeId,
                                    StepId = step.Id,
                                    Language = targetLang,
                                    TranslatedText = translatedText,
                                    TranslatedAt = DateTime.UtcNow,
                                    TranslationProvider = "gpt-4o",
                                    IsCoreSprache = true
                                };

                                _db.RecipeStepTranslations.Add(translation);
                            }
                        }

                        await _db.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation("  ✅ {Lang}: {Count} steps saved",
                            targetLang.ToUpper(), translations.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "  ❌ Failed to translate to {Lang}", targetLang);
                    }
                }

                // 3. Update Translation Status
                await UpdateTranslationStatusAsync(recipeId, cancellationToken);

                _logger.LogInformation("🎉 Recipe {RecipeId} now available in 4 languages", recipeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate recipe {RecipeId}", recipeId);
            }
        }

        public async Task<List<string>> TranslateStepsOnDemandAsync(
            int recipeId,
            string targetLanguage,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. Prüfe ob schon vorhanden
                var existingTranslations = await _db.RecipeStepTranslations
                    .Where(t => t.RecipeId == recipeId && t.Language == targetLanguage)
                    .OrderBy(t => t.Step.StepOrder)
                    .Select(t => t.TranslatedText)
                    .ToListAsync(cancellationToken);

                if (existingTranslations.Count > 0)
                {
                    _logger.LogInformation("✅ Cache hit for recipe {RecipeId} in {Lang}", recipeId, targetLanguage);
                    return existingTranslations;
                }

                // 2. Lade Original Steps
                var steps = await _db.RecipeSteps
                    .Where(s => s.RecipeId == recipeId)
                    .OrderBy(s => s.StepOrder)
                    .ToListAsync(cancellationToken);

                if (steps.Count == 0)
                {
                    return new List<string>();
                }

                var sourceLang = steps.First().SourceLanguage ?? "de";

                _logger.LogInformation("⏳ On-demand translating recipe {RecipeId} to {Lang}", recipeId, targetLanguage);

                // 3. Übersetze mit GPT-4o-mini (günstiger!)
                var translations = await TranslateBatchWithGPTAsync(
                    steps,
                    sourceLang,
                    targetLanguage,
                    useGPT4oMini: true,
                    cancellationToken);

                if (translations == null || translations.Count == 0)
                {
                    _logger.LogWarning("Translation returned empty for {Lang}", targetLanguage);
                    return new List<string>();
                }

                // 4. Speichere in DB
                foreach (var (step, translatedText) in translations)
                {
                    var translation = new RecipeStepTranslation
                    {
                        RecipeId = recipeId,
                        StepId = step.Id,
                        Language = targetLanguage,
                        TranslatedText = translatedText,
                        TranslatedAt = DateTime.UtcNow,
                        TranslationProvider = "gpt-4o-mini",
                        IsCoreSprache = false
                    };

                    _db.RecipeStepTranslations.Add(translation);
                }

                await _db.SaveChangesAsync(cancellationToken);

                // 5. Update Status
                await UpdateTranslationStatusAsync(recipeId, cancellationToken);

                _logger.LogInformation("✅ On-demand translation to {Lang} completed", targetLanguage);

                return translations.Select(t => t.translation).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed on-demand translation for recipe {RecipeId} to {Lang}", recipeId, targetLanguage);
                return new List<string>();
            }
        }

        public async Task<List<string>> GetStepsForLanguageAsync(
            int recipeId,
            string language,
            CancellationToken cancellationToken = default)
        {
            // Prüfe ob Steps in dieser Sprache existieren
            var steps = await _db.RecipeSteps
                .Where(s => s.RecipeId == recipeId)
                .OrderBy(s => s.StepOrder)
                .ToListAsync(cancellationToken);

            if (steps.Count == 0)
                return new List<string>();

            var sourceLang = steps.First().SourceLanguage ?? "de";

            // Source Language: Direkt aus RecipeStep
            if (language == sourceLang)
            {
                return steps.Select(s => s.StepText).ToList();
            }

            // Übersetzt: Aus RecipeStepTranslation
            var translations = await _db.RecipeStepTranslations
                .Where(t => t.RecipeId == recipeId && t.Language == language)
                .OrderBy(t => t.Step.StepOrder)
                .Select(t => t.TranslatedText)
                .ToListAsync(cancellationToken);

            return translations;
        }

        private async Task<List<(RecipeStep step, string translation)>?> TranslateBatchWithGPTAsync(
            List<RecipeStep> steps,
            string sourceLang,
            string targetLang,
            bool useGPT4oMini,
            CancellationToken cancellationToken)
        {
            // Gleiche Key-Logik wie andere Services (z.B. CaptionGenerationService)
            var apiKey = Environment.GetEnvironmentVariable("SecretKeyOpenAi")
                ?? _configuration["SecretKeyOpenAi"]
                ?? _configuration["OpenAI:ApiKey"]
                ?? _configuration["OpenAI_API_Key"];

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("OpenAI API Key not configured (SecretKeyOpenAi, OpenAI:ApiKey, or OpenAI_API_Key)");
                return null;
            }

            var sourceLangName = LanguageNames.GetValueOrDefault(sourceLang, "German");
            var targetLangName = LanguageNames.GetValueOrDefault(targetLang, "English");

            // Formatiere Steps mit "Schritt 1", "Schritt 2", etc.
            var stepsText = string.Join("\n\n", steps.Select(s =>
                $"Schritt {s.StepOrder}\n{s.StepText}"));

            var prompt = $@"Du bist ein professioneller Übersetzer für Kochrezepte.

Übersetze diese Zubereitungsschritte von {sourceLangName} nach {targetLangName}.

WICHTIGE REGELN:
1. ✅ Behalte die Struktur ""Schritt 1"", ""Schritt 2"", etc.
2. ✅ Behalte Emojis bei (♨️, 🍲, 🥄, etc.)
3. ✅ Verwende korrekten Imperativ (""Cut the onions"", nicht ""Cutting the onions"")
4. ✅ Behalte Artikel bei Zutaten (""die Zwiebeln"" → ""the onions"")
5. ✅ Verwende korrekte Koch-Terminologie
6. ✅ Behalte Temperaturangaben bei (180°C)
7. ✅ Behalte Zeitangaben bei (100-120 Minuten)
8. ✅ JEDER Schritt startet mit ""Schritt X"" auf einer neuen Zeile
9. ❌ Füge KEINE zusätzlichen Erklärungen hinzu

Schritte:
{stepsText}

Antworte NUR mit den übersetzten Schritten im EXAKT gleichen Format:";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var model = useGPT4oMini ? "gpt-4o-mini" : "gpt-4o";

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are a professional cooking recipe translator. Preserve exact formatting including 'Schritt X' labels, emojis, and cooking terminology."
                        },
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = 0.1
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = JsonContent.Create(requestBody);

                var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: cancellationToken);
                var gptResponse = responseBody?.Choices?.FirstOrDefault()?.Message?.Content ?? "";

                if (string.IsNullOrEmpty(gptResponse))
                {
                    _logger.LogWarning("GPT returned empty response");
                    return null;
                }

                // Parse Response
                return ParseGPTResponse(gptResponse, steps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GPT API call failed");
                return null;
            }
        }

        private List<(RecipeStep step, string translation)> ParseGPTResponse(
            string gptResponse,
            List<RecipeStep> originalSteps)
        {
            var result = new List<(RecipeStep, string)>();

            try
            {
                // Split by "Schritt X"
                var pattern = @"Schritt\s+(\d+)\s*\n(.+?)(?=\nSchritt\s+\d+|$)";
                var matches = Regex.Matches(gptResponse, pattern, RegexOptions.Singleline);

                foreach (Match match in matches)
                {
                    var stepNumber = int.Parse(match.Groups[1].Value);
                    var translatedText = match.Groups[2].Value.Trim();

                    // Finde Original Step
                    var originalStep = originalSteps.FirstOrDefault(s => s.StepOrder == stepNumber);
                    if (originalStep != null)
                    {
                        result.Add((originalStep, translatedText));
                    }
                }

                // Validierung
                if (result.Count != originalSteps.Count)
                {
                    _logger.LogWarning("Translation parsing mismatch. Expected {Expected}, got {Actual}",
                        originalSteps.Count, result.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse GPT response");
                return new List<(RecipeStep, string)>();
            }
        }

        private async Task UpdateTranslationStatusAsync(int recipeId, CancellationToken cancellationToken)
        {
            var status = await _db.RecipeTranslationStatuses.FindAsync(new object[] { recipeId }, cancellationToken);

            if (status == null)
            {
                status = new RecipeTranslationStatus { RecipeId = recipeId };
                _db.RecipeTranslationStatuses.Add(status);
            }

            // Prüfe welche Sprachen vorhanden sind
            var availableLanguages = await _db.RecipeStepTranslations
                .Where(t => t.RecipeId == recipeId)
                .Select(t => t.Language)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Source Language auch markieren
            var sourceSteps = await _db.RecipeSteps
                .Where(s => s.RecipeId == recipeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceSteps != null)
            {
                var sourceLang = sourceSteps.SourceLanguage ?? "de";
                if (!availableLanguages.Contains(sourceLang))
                {
                    availableLanguages.Add(sourceLang);
                }
            }

            // Update Flags
            status.HasDE = availableLanguages.Contains("de");
            status.HasEN = availableLanguages.Contains("en");
            status.HasESP = availableLanguages.Contains("esp");
            status.HasPRT = availableLanguages.Contains("prt");
            status.HasID = availableLanguages.Contains("id");
            status.HasNL = availableLanguages.Contains("nl");
            status.HasSV = availableLanguages.Contains("sv");
            status.HasDA = availableLanguages.Contains("da");
            status.HasNO = availableLanguages.Contains("no");
            status.HasMS = availableLanguages.Contains("ms");
            status.LastUpdated = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        // Helper Classes für OpenAI API
        private class OpenAIResponse
        {
            public List<OpenAIChoice>? Choices { get; set; }
        }

        private class OpenAIChoice
        {
            public OpenAIMessage? Message { get; set; }
        }

        private class OpenAIMessage
        {
            public string? Content { get; set; }
        }
    }
}
