using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Globalization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public sealed class MissingIngredientAiService : IMissingIngredientAiService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string ModelName = "gpt-5-mini";

        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<MissingIngredientAiService> _logger;

        public MissingIngredientAiService(
            HttpClient httpClient,
            ApplicationDbContext context,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<MissingIngredientAiService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task<MissingIngredientAiSuggestionResult> SuggestIngredientAsync(string ingredientName, CancellationToken cancellationToken = default)
        {
            var trimmedIngredientName = (ingredientName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedIngredientName))
            {
                throw new ArgumentException("Ingredient name is required.", nameof(ingredientName));
            }

            var apiKey =
                Environment.GetEnvironmentVariable("SecretKeyOpenAi") ??
                _configuration["SecretKeyOpenAi"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("SecretKeyOpenAi ist in Development nicht gesetzt. Verwende lokalen Debug-Fallback für {IngredientName}.", trimmedIngredientName);
                    return BuildDevelopmentFallbackResult(trimmedIngredientName);
                }

                throw new InvalidOperationException("Die Umgebungsvariable oder Konfiguration 'SecretKeyOpenAi' ist nicht gesetzt.");
            }

            var groups = await _context.Group
                .OrderBy(g => g.Id)
                .Select(g => new { g.Id, g.Name, g.Icon })
                .ToListAsync(cancellationToken);

            var groupPrompt = groups.Count == 0
                ? "Keine Gruppen verfügbar."
                : string.Join(", ", groups.Select(g => $"{g.Id}={g.Name} ({g.Icon ?? "-"})"));

            var requestBody = new
            {
                model = ModelName,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text =
                                    "Du hilfst beim Klassifizieren neuer Lebensmittel für eine Rezeptdatenbank. " +
                                    "Liefere ausschließlich valides JSON im vorgegebenen Schema. " +
                                    "Alle Namens- und Genus-Felder müssen befüllt sein. " +
                                    "Übersetze die Bezeichnung natürlich in jede Zielsprache. " +
                                    "Wiederhole denselben Namen nur dann in mehreren Sprachen, wenn der Begriff dort wirklich üblich ist. " +
                                    "Kopiere nicht blind denselben Namen in alle Sprachfelder. " +
                                    "Verwende für Genus nur kurze Sprach-Codes. " +
                                    "Deutsch: m/f/n/pl, Englisch: -, Spanisch/Portugiesisch: m/f/pl, Niederländisch: de/het/pl, Schwedisch: en/ett/pl, Dänisch: en/et/pl, Norwegisch: en/ei/et/pl, Indonesisch/Malaiisch: -. " +
                                    "Nutze nur vorhandene Gruppen-IDs. Erfinde keine Gruppe. " +
                                    "Bool-Felder müssen vorsichtig gesetzt werden: lieber false als raten. " +
                                    "Bestimme Nährwerte möglichst pro 100g. Wenn du unsicher bist, gib konservative plausible Standardwerte an. " +
                                    $"Verfügbare Gruppen: {groupPrompt}"
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text = $"Die vom User eingegebene Zutat lautet: '{trimmedIngredientName}'. " +
                                       "Schlage die wahrscheinlich gemeinte Zutat vor und fülle die Datenbankfelder dafür aus."
                            }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "ingredient_database_suggestion",
                        strict = true,
                        schema = BuildSchema()
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI ingredient suggestion failed: {StatusCode} {Body}", response.StatusCode, responseContent);
                throw new InvalidOperationException("OpenAI-Antwort konnte nicht geladen werden.");
            }

            using var document = JsonDocument.Parse(responseContent);
            var outputText = TryExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogError("OpenAI ingredient suggestion returned no output_text. Response: {Body}", responseContent);
                throw new InvalidOperationException("OpenAI hat keinen strukturierten Vorschlag zurückgegeben.");
            }

            var proposal = JsonSerializer.Deserialize<MissingIngredientAiProposal>(
                outputText,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("OpenAI-Vorschlag konnte nicht gelesen werden.");

            NormalizeProposal(proposal, trimmedIngredientName, groups.Select(g => g.Id).ToHashSet());

            return new MissingIngredientAiSuggestionResult
            {
                Model = ModelName,
                RawJson = outputText,
                Proposal = proposal
            };
        }

        private static MissingIngredientAiSuggestionResult BuildDevelopmentFallbackResult(string ingredientName)
        {
            var lower = ingredientName.Trim().ToLowerInvariant();
            var isLiquid = lower.Contains("saft") || lower.Contains("juice") || lower.Contains("milch") || lower.Contains("sauce");
            var isPowder = lower.Contains("mehl") || lower.Contains("powder") || lower.Contains("pulver");
            var isFat = lower.Contains("öl") || lower.Contains("oil") || lower.Contains("butter");

            var proposal = new MissingIngredientAiProposal
            {
                OriginalQuery = ingredientName,
                CanonicalName = NormalizeIngredientName(ingredientName),
                ConfirmationPrompt = $"Debug-Modus: Meintest du {ingredientName}?",
                Notes = "Lokaler Debug-Fallback ohne echten OpenAI-Call. Setze SecretKeyOpenAi für echte KI-Vorschläge.",
                Confidence = 0.35m,
                IsDebugFallback = true,
                Icon = isLiquid ? "🥤" : isPowder ? "🥣" : isFat ? "🧈" : "🥬",
                GroupId = isPowder ? 8 : 7,
                Name_DE = NormalizeIngredientName(ingredientName),
                Name_EN = NormalizeIngredientName(ingredientName),
                Name_PRT = NormalizeIngredientName(ingredientName),
                Name_ESP = NormalizeIngredientName(ingredientName),
                Name_ID = NormalizeIngredientName(ingredientName),
                Name_NL = NormalizeIngredientName(ingredientName),
                Name_SE = NormalizeIngredientName(ingredientName),
                Name_DK = NormalizeIngredientName(ingredientName),
                Name_NO = NormalizeIngredientName(ingredientName),
                Name_MS = NormalizeIngredientName(ingredientName),
                Genus_DE = "-",
                Genus_EN = "-",
                Genus_ESP = "-",
                Genus_PRT = "-",
                Genus_ID = "-",
                Genus_MS = "-",
                Genus_NL = "-",
                Genus_SE = "-",
                Genus_DK = "-",
                Genus_NO = "-",
                Calories_a_100g = 100,
                Weight_per_piece = 0,
                Fat_a_100g = isFat ? 80m : 1m,
                Saturated_fat_a_100g = isFat ? 20m : 0.2m,
                Carbohydrates_a_100g = isPowder ? 70m : 10m,
                Sugar_a_100g = 3m,
                Salt_a_100g = 0.1m,
                Protein_a_100g = 2m,
                Fiber_a_100g = 2m,
                is_liquid = isLiquid,
                is_hard = !isLiquid && !isPowder,
                is_soft = false,
                is_fat = isFat,
                is_peelable = false,
                is_cuttable = !isLiquid && !isPowder,
                is_grateable = false,
                is_fryable = !isLiquid && !isPowder,
                is_roastable = !isLiquid && !isPowder,
                is_grillable = !isLiquid && !isPowder,
                is_steamable = !isLiquid && !isPowder,
                is_boilable = !isPowder,
                is_searable = !isLiquid && !isPowder,
                is_poachable = isLiquid,
                is_smokable = false,
                is_flambeable = false,
                is_blendable = true,
                is_powder = isPowder
            };

            return new MissingIngredientAiSuggestionResult
            {
                Model = "debug-fallback",
                RawJson = JsonSerializer.Serialize(proposal),
                Proposal = proposal
            };
        }

        private static object BuildSchema()
        {
            return new
            {
                type = "object",
                additionalProperties = false,
                required = new[]
                {
                    "originalQuery",
                    "canonicalName",
                    "confirmationPrompt",
                    "notes",
                    "confidence",
                    "icon",
                    "groupId",
                    "name_DE",
                    "name_EN",
                    "name_PRT",
                    "name_ESP",
                    "name_ID",
                    "name_NL",
                    "name_SE",
                    "name_DK",
                    "name_NO",
                    "name_MS",
                    "genus_DE",
                    "genus_EN",
                    "genus_ESP",
                    "genus_PRT",
                    "genus_ID",
                    "genus_MS",
                    "genus_NL",
                    "genus_SE",
                    "genus_DK",
                    "genus_NO",
                    "calories_a_100g",
                    "weight_per_piece",
                    "fat_a_100g",
                    "saturated_fat_a_100g",
                    "carbohydrates_a_100g",
                    "sugar_a_100g",
                    "salt_a_100g",
                    "protein_a_100g",
                    "fiber_a_100g",
                    "is_liquid",
                    "is_hard",
                    "is_soft",
                    "is_fat",
                    "is_peelable",
                    "is_cuttable",
                    "is_grateable",
                    "is_fryable",
                    "is_roastable",
                    "is_grillable",
                    "is_steamable",
                    "is_boilable",
                    "is_searable",
                    "is_poachable",
                    "is_smokable",
                    "is_flambeable",
                    "is_blendable",
                    "is_powder"
                },
                properties = new
                {
                    originalQuery = new { type = "string" },
                    canonicalName = new { type = "string" },
                    confirmationPrompt = new { type = "string" },
                    notes = new { type = "string" },
                    confidence = new { type = "number" },
                    icon = new { type = "string" },
                    groupId = new
                    {
                        anyOf = new object[]
                        {
                            new { type = "integer" },
                            new { type = "null" }
                        }
                    },
                    name_DE = new { type = "string" },
                    name_EN = new { type = "string" },
                    name_PRT = new { type = "string" },
                    name_ESP = new { type = "string" },
                    name_ID = new { type = "string" },
                    name_NL = new { type = "string" },
                    name_SE = new { type = "string" },
                    name_DK = new { type = "string" },
                    name_NO = new { type = "string" },
                    name_MS = new { type = "string" },
                    genus_DE = new { type = "string", @enum = new[] { "m", "f", "n", "pl", "-" } },
                    genus_EN = new { type = "string", @enum = new[] { "-" } },
                    genus_ESP = new { type = "string", @enum = new[] { "m", "f", "pl", "-" } },
                    genus_PRT = new { type = "string", @enum = new[] { "m", "f", "pl", "-" } },
                    genus_ID = new { type = "string", @enum = new[] { "-" } },
                    genus_MS = new { type = "string", @enum = new[] { "-" } },
                    genus_NL = new { type = "string", @enum = new[] { "de", "het", "pl", "-" } },
                    genus_SE = new { type = "string", @enum = new[] { "en", "ett", "pl", "-" } },
                    genus_DK = new { type = "string", @enum = new[] { "en", "et", "pl", "-" } },
                    genus_NO = new { type = "string", @enum = new[] { "en", "ei", "et", "pl", "-" } },
                    calories_a_100g = new { type = "integer" },
                    weight_per_piece = new { type = "integer" },
                    fat_a_100g = new { type = "number" },
                    saturated_fat_a_100g = new { type = "number" },
                    carbohydrates_a_100g = new { type = "number" },
                    sugar_a_100g = new { type = "number" },
                    salt_a_100g = new { type = "number" },
                    protein_a_100g = new { type = "number" },
                    fiber_a_100g = new { type = "number" },
                    is_liquid = new { type = "boolean" },
                    is_hard = new { type = "boolean" },
                    is_soft = new { type = "boolean" },
                    is_fat = new { type = "boolean" },
                    is_peelable = new { type = "boolean" },
                    is_cuttable = new { type = "boolean" },
                    is_grateable = new { type = "boolean" },
                    is_fryable = new { type = "boolean" },
                    is_roastable = new { type = "boolean" },
                    is_grillable = new { type = "boolean" },
                    is_steamable = new { type = "boolean" },
                    is_boilable = new { type = "boolean" },
                    is_searable = new { type = "boolean" },
                    is_poachable = new { type = "boolean" },
                    is_smokable = new { type = "boolean" },
                    is_flambeable = new { type = "boolean" },
                    is_blendable = new { type = "boolean" },
                    is_powder = new { type = "boolean" }
                }
            };
        }

        private static string TryExtractOutputText(JsonElement root)
        {
            if (root.TryGetProperty("output_text", out var outputTextElement) &&
                outputTextElement.ValueKind == JsonValueKind.String)
            {
                return outputTextElement.GetString() ?? string.Empty;
            }

            if (!root.TryGetProperty("output", out var outputElement) || outputElement.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (var item in outputElement.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentElement.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        return textElement.GetString() ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private static void NormalizeProposal(MissingIngredientAiProposal proposal, string originalQuery, HashSet<int> validGroupIds)
        {
            proposal.OriginalQuery = CoalesceText(proposal.OriginalQuery, originalQuery);
            proposal.CanonicalName = CoalesceText(proposal.CanonicalName, proposal.Name_DE, proposal.Name_EN, originalQuery);
            proposal.Name_DE = NormalizeIngredientName(CoalesceText(proposal.Name_DE, proposal.CanonicalName, originalQuery));
            proposal.Name_EN = NormalizeIngredientName(CoalesceText(proposal.Name_EN, proposal.CanonicalName, proposal.Name_DE));
            proposal.Name_PRT = NormalizeIngredientName(CoalesceText(proposal.Name_PRT, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_ESP = NormalizeIngredientName(CoalesceText(proposal.Name_ESP, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_ID = NormalizeIngredientName(CoalesceText(proposal.Name_ID, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_NL = NormalizeIngredientName(CoalesceText(proposal.Name_NL, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_SE = NormalizeIngredientName(CoalesceText(proposal.Name_SE, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_DK = NormalizeIngredientName(CoalesceText(proposal.Name_DK, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_NO = NormalizeIngredientName(CoalesceText(proposal.Name_NO, proposal.Name_EN, proposal.Name_DE));
            proposal.Name_MS = NormalizeIngredientName(CoalesceText(proposal.Name_MS, proposal.Name_EN, proposal.Name_DE));

            proposal.Genus_DE = NormalizeGermanGenus(proposal.Genus_DE);
            proposal.Genus_EN = NormalizeEnglishLikeGenus();
            proposal.Genus_ESP = NormalizeRomanceGenus(proposal.Genus_ESP);
            proposal.Genus_PRT = NormalizeRomanceGenus(proposal.Genus_PRT);
            proposal.Genus_ID = NormalizeEnglishLikeGenus();
            proposal.Genus_MS = NormalizeEnglishLikeGenus();
            proposal.Genus_NL = NormalizeDutchGenus(proposal.Genus_NL);
            proposal.Genus_SE = NormalizeScandinavianGenus(proposal.Genus_SE, "ett");
            proposal.Genus_DK = NormalizeScandinavianGenus(proposal.Genus_DK, "et");
            proposal.Genus_NO = NormalizeNorwegianGenus(proposal.Genus_NO);

            proposal.ConfirmationPrompt = CoalesceText(
                proposal.ConfirmationPrompt,
                $"Meintest du {proposal.Name_DE}?");
            proposal.Notes = (proposal.Notes ?? string.Empty).Trim();
            proposal.Icon = CoalesceText(proposal.Icon, "🥣");
            proposal.Confidence = Clamp(proposal.Confidence, 0m, 1m);
            proposal.Calories_a_100g = Math.Max(0, proposal.Calories_a_100g);
            proposal.Weight_per_piece = Math.Max(0, proposal.Weight_per_piece);
            proposal.Fat_a_100g = Clamp(proposal.Fat_a_100g, 0m, 999m);
            proposal.Saturated_fat_a_100g = Clamp(proposal.Saturated_fat_a_100g, 0m, 999m);
            proposal.Carbohydrates_a_100g = Clamp(proposal.Carbohydrates_a_100g, 0m, 999m);
            proposal.Sugar_a_100g = Clamp(proposal.Sugar_a_100g, 0m, 999m);
            proposal.Salt_a_100g = Clamp(proposal.Salt_a_100g, 0m, 999m);
            proposal.Protein_a_100g = Clamp(proposal.Protein_a_100g, 0m, 999m);
            proposal.Fiber_a_100g = Clamp(proposal.Fiber_a_100g, 0m, 999m);

            if (proposal.GroupId.HasValue && !validGroupIds.Contains(proposal.GroupId.Value))
            {
                proposal.GroupId = null;
            }
        }

        private static string CoalesceText(params string?[] values)
        {
            foreach (var value in values)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }

        private static string NormalizeGermanGenus(string? genus)
        {
            var value = NormalizeGenusToken(genus);
            if (value is "m" or "masc" or "masculine" or "maskulin" or "male" or "der")
            {
                return "m";
            }

            if (value is "f" or "fem" or "feminine" or "feminin" or "female" or "die")
            {
                return "f";
            }

            if (value is "n" or "neut" or "neuter" or "neutral" or "das")
            {
                return "n";
            }

            if (value is "pl" or "plural" or "pluralis")
            {
                return "pl";
            }

            return "-";
        }

        private static string NormalizeRomanceGenus(string? genus)
        {
            var value = NormalizeGenusToken(genus);
            if (value is "m" or "masc" or "masculine" or "masculino" or "el" or "o")
            {
                return "m";
            }

            if (value is "f" or "fem" or "feminine" or "feminino" or "la" or "a")
            {
                return "f";
            }

            if (value is "pl" or "plural" or "los" or "las" or "os" or "as")
            {
                return "pl";
            }

            return "-";
        }

        private static string NormalizeDutchGenus(string? genus)
        {
            var value = NormalizeGenusToken(genus);
            if (value is "de" or "common" or "c" or "m" or "f")
            {
                return "de";
            }

            if (value is "het" or "n" or "neut" or "neuter")
            {
                return "het";
            }

            if (value is "pl" or "plural")
            {
                return "pl";
            }

            return "-";
        }

        private static string NormalizeScandinavianGenus(string? genus, string neuterCode)
        {
            var value = NormalizeGenusToken(genus);
            if (value is "en" or "common" or "c" or "m" or "f")
            {
                return "en";
            }

            if (value is "pl" or "plural")
            {
                return "pl";
            }

            if (value is "ett" or "et" or "n" or "neut" or "neuter")
            {
                return neuterCode;
            }

            return "-";
        }

        private static string NormalizeNorwegianGenus(string? genus)
        {
            var value = NormalizeGenusToken(genus);
            if (value is "ei" or "f" or "fem" or "feminine")
            {
                return "ei";
            }

            if (value is "en" or "common" or "c" or "m" or "masc" or "masculine")
            {
                return "en";
            }

            if (value is "et" or "n" or "neut" or "neuter")
            {
                return "et";
            }

            if (value is "pl" or "plural")
            {
                return "pl";
            }

            return "-";
        }

        private static string NormalizeEnglishLikeGenus()
        {
            return "-";
        }

        private static string NormalizeGenusToken(string? genus)
        {
            var trimmed = (genus ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "-";
            }

            var value = trimmed.ToLowerInvariant();
            return value is "-" or "none" or "unknown" or "na" or "n/a"
                ? "-"
                : value;
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string NormalizeIngredientName(string value)
        {
            var trimmed = CoalesceText(value);
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var collapsed = string.Join(" ", trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return CultureInfo.GetCultureInfo("de-AT").TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
        }
    }
}
