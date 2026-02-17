using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DelikatessenDrehbuch.Services
{
    public class StepTemplateResolver : IStepTemplateResolver
    {
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private const string StepsCacheKey = "StepTemplates";
        private const string GrammarCacheKey = "IngredientGrammar";
        private const string GroupDefaultsCacheKey = "GroupCookingDefaults";
        private static readonly string[] SupportedLanguages = { "de", "en", "pt", "es" };

        public StepTemplateResolver(IWebHostEnvironment env, IMemoryCache cache)
        {
            _env = env;
            _cache = cache;
        }

        public string GetStep(int stepId, string lang)
        {
            lang = NormalizeLang(lang);
            var steps = LoadStepTemplates();
            var step = steps.FirstOrDefault(s => s.Id == stepId);
            if (step == null) return string.Empty;

            return GetStepText(step, lang);
        }

        public List<ResolvedStep> GetSteps(List<int> stepIds, string lang)
        {
            lang = NormalizeLang(lang);
            var steps = LoadStepTemplates();
            var result = new List<ResolvedStep>();

            foreach (var stepId in stepIds)
            {
                var step = steps.FirstOrDefault(s => s.Id == stepId);
                if (step == null) continue;

                result.Add(new ResolvedStep
                {
                    Id = step.Id,
                    Action = step.Action,
                    Phase = step.Phase,
                    Text = GetStepText(step, lang),
                    LinkedIngredients = step.LinkedIngredients ?? new List<int>(),
                    Equipment = step.Equipment ?? new List<string>()
                });
            }

            return result;
        }

        public string GetIngredientWithArticle(int ingredientId, string lang, string grammaticalCase = "nom")
        {
            lang = NormalizeLang(lang);
            var grammar = LoadIngredientGrammar();
            var ingredient = grammar.FirstOrDefault(g => g.Id == ingredientId);
            if (ingredient == null) return string.Empty;

            return lang switch
            {
                "de" => grammaticalCase switch
                {
                    "akk" => ingredient.De?.Akk ?? ingredient.De?.Name ?? string.Empty,
                    "dat" => ingredient.De?.Dat ?? ingredient.De?.Name ?? string.Empty,
                    _ => ingredient.De?.Nom ?? ingredient.De?.Name ?? string.Empty
                },
                "pt" => ingredient.Pt?.Art ?? ingredient.Pt?.Name ?? string.Empty,
                "es" => ingredient.Es?.Art ?? ingredient.Es?.Name ?? string.Empty,
                _ => ingredient.En?.Name ?? ingredient.De?.Name ?? string.Empty
            };
        }

        public string GetDefaultCutStyle(int ingredientId, string lang)
        {
            lang = NormalizeLang(lang);
            var grammar = LoadIngredientGrammar();
            var ingredient = grammar.FirstOrDefault(g => g.Id == ingredientId);
            if (ingredient == null) return string.Empty;

            var groupId = ingredient.GroupId;
            var groups = LoadGroupDefaults();
            var group = groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group == null || string.IsNullOrEmpty(group.DefaultCut)) return string.Empty;

            var cutStyle = group.CutStyles?.FirstOrDefault(c => c.Id == group.DefaultCut);
            if (cutStyle == null) return string.Empty;

            return GetLocalizedText(cutStyle, lang);
        }

        public string GetIngredientName(int ingredientId, string lang)
        {
            lang = NormalizeLang(lang);
            var grammar = LoadIngredientGrammar();
            var ingredient = grammar.FirstOrDefault(g => g.Id == ingredientId);
            if (ingredient == null) return string.Empty;

            return lang switch
            {
                "de" => ingredient.De?.Name ?? string.Empty,
                "en" => ingredient.En?.Name ?? ingredient.De?.Name ?? string.Empty,
                "pt" => ingredient.Pt?.Name ?? ingredient.De?.Name ?? string.Empty,
                "es" => ingredient.Es?.Name ?? ingredient.De?.Name ?? string.Empty,
                _ => ingredient.De?.Name ?? string.Empty
            };
        }

        public List<int> GetStepIdsByAction(string action)
        {
            var steps = LoadStepTemplates();
            return steps
                .Where(s => s.Action.Equals(action, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .ToList();
        }

        public GroupCookingDefaults GetGroupDefaults(int groupId, string lang)
        {
            lang = NormalizeLang(lang);
            var groups = LoadGroupDefaults();
            var group = groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group == null) return new GroupCookingDefaults { GroupId = groupId };

            return new GroupCookingDefaults
            {
                GroupId = group.GroupId,
                GroupName = GetGroupName(group, lang),
                TypicalActionChain = group.TypicalActionChain ?? new List<string>(),
                DefaultCut = group.DefaultCut,
                CutStyles = group.CutStyles?.Select(c => new CutStyleOption
                {
                    Id = c.Id,
                    Text = GetLocalizedText(c, lang)
                }).ToList() ?? new List<CutStyleOption>(),
                PrepMethods = group.PrepMethods?.Select(p => new PrepMethodOption
                {
                    Id = p.Id,
                    Text = GetLocalizedText(p, lang)
                }).ToList() ?? new List<PrepMethodOption>()
            };
        }

        // ============================================================
        // Private helpers
        // ============================================================

        private static string NormalizeLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "de";
            lang = lang.ToLowerInvariant().Trim();
            // Map common aliases
            return lang switch
            {
                "prt" or "pt-br" or "pt-pt" => "pt",
                "esp" or "es-es" => "es",
                "eng" or "en-us" or "en-gb" => "en",
                "deu" or "de-de" or "de-at" => "de",
                _ => SupportedLanguages.Contains(lang) ? lang : "de"
            };
        }

        private static string GetStepText(StepTemplateEntry step, string lang)
        {
            var text = lang switch
            {
                "en" => step.En,
                "pt" => step.Pt,
                "es" => step.Es,
                _ => step.De
            };

            // Fallback to German if translation is empty
            return string.IsNullOrWhiteSpace(text) ? step.De : text;
        }

        private static string GetLocalizedText(CutStyleRaw cutStyle, string lang)
        {
            return lang switch
            {
                "en" => cutStyle.En ?? cutStyle.De,
                "pt" => cutStyle.Pt ?? cutStyle.De,
                "es" => cutStyle.Es ?? cutStyle.De,
                _ => cutStyle.De
            } ?? string.Empty;
        }

        private static string GetLocalizedText(PrepMethodRaw prepMethod, string lang)
        {
            return lang switch
            {
                "en" => prepMethod.En ?? prepMethod.De,
                "pt" => prepMethod.Pt ?? prepMethod.De,
                "es" => prepMethod.Es ?? prepMethod.De,
                _ => prepMethod.De
            } ?? string.Empty;
        }

        private static string GetGroupName(GroupDefaultsRaw group, string lang)
        {
            return lang switch
            {
                "en" => group.GroupNameEn ?? group.GroupNameDe,
                "pt" => group.GroupNamePt ?? group.GroupNameDe,
                "es" => group.GroupNameEs ?? group.GroupNameDe,
                _ => group.GroupNameDe
            } ?? string.Empty;
        }

        // ============================================================
        // JSON loading with caching
        // ============================================================

        private List<StepTemplateEntry> LoadStepTemplates()
        {
            return _cache.GetOrCreate(StepsCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                var path = Path.Combine(_env.ContentRootPath, "Data", "exports", "step_templates.json");
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<StepTemplatesFile>(json, JsonOpts);
                return data?.StepTemplates ?? new List<StepTemplateEntry>();
            })!;
        }

        private List<IngredientGrammarEntry> LoadIngredientGrammar()
        {
            return _cache.GetOrCreate(GrammarCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                var path = Path.Combine(_env.ContentRootPath, "Data", "exports", "ingredient_grammar.json");
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<IngredientGrammarFile>(json, JsonOpts);
                return data?.Ingredients ?? new List<IngredientGrammarEntry>();
            })!;
        }

        private List<GroupDefaultsRaw> LoadGroupDefaults()
        {
            return _cache.GetOrCreate(GroupDefaultsCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                var path = Path.Combine(_env.ContentRootPath, "Data", "exports", "group_cooking_defaults.json");
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<GroupDefaultsFile>(json, JsonOpts);
                return data?.Groups ?? new List<GroupDefaultsRaw>();
            })!;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // ============================================================
        // Internal JSON DTOs
        // ============================================================

        private class StepTemplatesFile
        {
            [JsonPropertyName("step_templates")]
            public List<StepTemplateEntry> StepTemplates { get; set; } = new();
        }

        private class StepTemplateEntry
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("action")]
            public string Action { get; set; } = string.Empty;

            [JsonPropertyName("phase")]
            public int Phase { get; set; }

            [JsonPropertyName("linked_ingredients")]
            public List<int> LinkedIngredients { get; set; } = new();

            [JsonPropertyName("equipment")]
            public List<string> Equipment { get; set; } = new();

            [JsonPropertyName("de")]
            public string De { get; set; } = string.Empty;

            [JsonPropertyName("en")]
            public string En { get; set; } = string.Empty;

            [JsonPropertyName("pt")]
            public string Pt { get; set; } = string.Empty;

            [JsonPropertyName("es")]
            public string Es { get; set; } = string.Empty;
        }

        private class IngredientGrammarFile
        {
            [JsonPropertyName("ingredients")]
            public List<IngredientGrammarEntry> Ingredients { get; set; } = new();
        }

        private class IngredientGrammarEntry
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("group_id")]
            public int GroupId { get; set; }

            [JsonPropertyName("de")]
            public IngredientGrammarDe De { get; set; }

            [JsonPropertyName("en")]
            public IngredientGrammarSimple En { get; set; }

            [JsonPropertyName("pt")]
            public IngredientGrammarWithArticle Pt { get; set; }

            [JsonPropertyName("es")]
            public IngredientGrammarWithArticle Es { get; set; }
        }

        private class IngredientGrammarDe
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("genus")]
            public string Genus { get; set; } = string.Empty;

            [JsonPropertyName("nom")]
            public string Nom { get; set; } = string.Empty;

            [JsonPropertyName("akk")]
            public string Akk { get; set; } = string.Empty;

            [JsonPropertyName("dat")]
            public string Dat { get; set; } = string.Empty;

            [JsonPropertyName("pron")]
            public string Pron { get; set; } = string.Empty;
        }

        private class IngredientGrammarSimple
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private class IngredientGrammarWithArticle
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("genus")]
            public string Genus { get; set; } = string.Empty;

            [JsonPropertyName("art")]
            public string Art { get; set; } = string.Empty;
        }

        private class GroupDefaultsFile
        {
            [JsonPropertyName("groups")]
            public List<GroupDefaultsRaw> Groups { get; set; } = new();
        }

        private class GroupDefaultsRaw
        {
            [JsonPropertyName("group_id")]
            public int GroupId { get; set; }

            [JsonPropertyName("group_name_de")]
            public string GroupNameDe { get; set; } = string.Empty;

            [JsonPropertyName("group_name_en")]
            public string GroupNameEn { get; set; } = string.Empty;

            [JsonPropertyName("group_name_pt")]
            public string GroupNamePt { get; set; } = string.Empty;

            [JsonPropertyName("group_name_es")]
            public string GroupNameEs { get; set; } = string.Empty;

            [JsonPropertyName("typical_action_chain")]
            public List<string> TypicalActionChain { get; set; } = new();

            [JsonPropertyName("cut_styles")]
            public List<CutStyleRaw> CutStyles { get; set; } = new();

            [JsonPropertyName("default_cut")]
            public string DefaultCut { get; set; }

            [JsonPropertyName("prep_methods")]
            public List<PrepMethodRaw> PrepMethods { get; set; } = new();
        }

        private class CutStyleRaw
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("de")]
            public string De { get; set; }

            [JsonPropertyName("en")]
            public string En { get; set; }

            [JsonPropertyName("pt")]
            public string Pt { get; set; }

            [JsonPropertyName("es")]
            public string Es { get; set; }
        }

        private class PrepMethodRaw
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("de")]
            public string De { get; set; }

            [JsonPropertyName("en")]
            public string En { get; set; }

            [JsonPropertyName("pt")]
            public string Pt { get; set; }

            [JsonPropertyName("es")]
            public string Es { get; set; }
        }
    }
}
