using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class RecipeStepGeneratorService : IRecipeStepGeneratorService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";
        private const string DefaultModel = "gpt-4o-mini"; // Fast + Cheap für Step-Generation
        private const int TimeoutSeconds = 90; // Erhöht für große Prompts mit allen SmartSteps

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<RecipeStepGeneratorService> _logger;
        private readonly IHostEnvironment _environment;

        public RecipeStepGeneratorService(
            HttpClient httpClient,
            IConfiguration config,
            IHostEnvironment environment,
            ILogger<RecipeStepGeneratorService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _environment = environment;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        public async Task<GeneratedStepsResult> GenerateStepsAsync(
            string recipeTitle,
            string category,
            List<string> ingredientNames,
            string language,
            string? instructionsText = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("📝 Starting AI step generation - Recipe: '{Title}', Language: {Lang}, Mode: {Mode}",
                recipeTitle, language, string.IsNullOrWhiteSpace(instructionsText) ? "Auto" : "Text-based");

            try
            {
                var apiKey = _config["OpenAI:ApiKey"] ?? _config["SecretKeyOpenAi"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError("OpenAI API key is missing in configuration");
                    throw new InvalidOperationException("OpenAI API key is missing");
                }


                // Load available SmartSteps from master_steps.json
                var masterSteps = LoadMasterSteps(language);

                // Build AI prompt (different for text-based mode)
                var prompt = string.IsNullOrWhiteSpace(instructionsText)
                    ? BuildPrompt(recipeTitle, category, ingredientNames, masterSteps, language)
                    : BuildPromptFromInstructions(recipeTitle, category, instructionsText, masterSteps, language);

                // Call OpenAI with structured output
                var response = await CallOpenAiAsync(apiKey, prompt, cancellationToken);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate steps for recipe: {Title}", recipeTitle);
                return new GeneratedStepsResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private List<SmartStepInfo> LoadMasterSteps(string language)
        {
            var langKey = NormalizeLanguageKey(language);
            var filePath = Path.Combine(_environment.ContentRootPath, $"wwwroot/data/master_steps.{langKey}.json");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Master steps file not found for language {Lang}, falling back to DE", langKey);
                filePath = Path.Combine(_environment.ContentRootPath, "wwwroot/data/master_steps.de.json");
            }

            var json = File.ReadAllText(filePath);
            var doc = JsonSerializer.Deserialize<MasterStepsDocument>(json);

            return doc?.MasterSteps?
                .Select(s => new SmartStepInfo
                {
                    MasterId = s.MasterId,
                    Phase = s.Phase,
                    Action = s.Action,
                    Description = s.Description,
                    Variables = s.Variables ?? new List<string>(),
                    RequiredVariables = s.RequiredVariables ?? new List<string>(),
                    Template = s.Template
                })
                .ToList() ?? new List<SmartStepInfo>();
        }

        private string BuildPrompt(
            string recipeTitle,
            string category,
            List<string> ingredientNames,
            List<SmartStepInfo> masterSteps,
            string language)
        {
            var languageName = GetLanguageName(language);
            var ingredientList = string.Join("\n", ingredientNames.Select((ing, i) => $"{i + 1}. {ing}"));

            // Group steps by phase for better overview
            var stepsByPhase = masterSteps
                .GroupBy(s => s.Phase)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Phase = g.Key,
                    PhaseName = GetPhaseName(g.Key, language),
                    Steps = g.Select(s => new
                    {
                        s.MasterId,
                        s.Action,
                        s.Description,
                        Variables = string.Join(", ", s.RequiredVariables),
                        Example = s.Template
                    }).ToList()
                })
                .ToList();

            var stepsJson = JsonSerializer.Serialize(stepsByPhase, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return $@"You are a professional recipe assistant. Generate cooking steps for a recipe.

**Recipe Information:**
- Title: {recipeTitle}
- Category: {category}
- Ingredients:
{ingredientList}

**Your Task:**
1. Analyze the recipe type and ingredients
2. Select appropriate SmartSteps from the available steps (see JSON below)
3. For each step, fill in the required variables with specific values from the ingredients
4. Order steps logically by phase (0=prep, 1=preparation, 2=cooking, 3=finishing, 4=serving)
5. Generate 6-12 steps total (not too many, not too few)

**Guidelines:**
- Use ONLY the MasterStepIds from the available steps
- **PREFER simple steps with empty required_variables** (e.g., COOK_BLEND_SIMPLE_01, COOK_BOIL_SIMPLE_01, COOK_REDUCE_HEAT_01) - they have no variables and work perfectly!
- If no simple step matches, use steps with variables and fill ALL required_variables with CONCRETE values (not placeholders!)
- For {{{{ingredient}}}}: use exact ingredient name WITH CORRECT ARTICLE and CASE (e.g., ""die Hähnchenbrust"", ""den Spargel"", ""dem Topf"")
- For {{{{action}}}}: use IMPERATIVE VERB FORM (e.g., ""dünste"" not ""dünsten"", ""schneide"" not ""schneiden"")
- For {{{{shape}}}}: specify cut shape (e.g., ""Würfel"", ""cubes"", ""strips"")
- For {{{{duration}}}}: give realistic time (e.g., ""5 Minuten"", ""5 minutes"")
- For {{{{temp}}}}: give temperature (e.g., ""180°C"", ""medium heat"", ""mittlere Hitze"")
- **CRITICAL: Check grammar** - correct articles (der/die/das/den/dem), verb forms (imperative), and avoid redundancy
- For soups/sauces: Use COOK_BLEND_SIMPLE_01 for blending/pureeing
- Steps must be in {languageName}
- Be specific and practical for home cooks

**Available SmartSteps by Phase:**
{stepsJson}

**Output Format:**
Return ONLY valid JSON matching the schema (enforced).
";
        }

        private string BuildPromptFromInstructions(
            string recipeTitle,
            string category,
            string instructionsText,
            List<SmartStepInfo> masterSteps,
            string language)
        {
            var languageName = GetLanguageName(language);

            // Group steps by phase for better overview
            var stepsByPhase = masterSteps
                .GroupBy(s => s.Phase)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Phase = g.Key,
                    PhaseName = GetPhaseName(g.Key, language),
                    Steps = g.Select(s => new
                    {
                        s.MasterId,
                        s.Action,
                        s.Description,
                        Variables = string.Join(", ", s.RequiredVariables),
                        Example = s.Template
                    }).ToList()
                })
                .ToList();

            var stepsJson = JsonSerializer.Serialize(stepsByPhase, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return $@"You are a professional recipe assistant. Convert user-provided cooking instructions into structured SmartSteps.

**Recipe Information:**
- Title: {recipeTitle}
- Category: {category}

**User's Instructions (copy-pasted text):**
{instructionsText}

**Your Task:**
1. Analyze the user's instructions and break them down into logical cooking steps
2. Match each step to appropriate SmartSteps from the available library (see JSON below)
3. Extract and fill in the required variables for each step from the user's text
4. Order steps logically by phase (0=prep, 1=preparation, 2=cooking, 3=finishing, 4=serving)
5. Generate 4-15 steps depending on the complexity

**Guidelines:**
- Use ONLY the MasterStepIds from the available steps
- **PREFER simple steps with empty required_variables** (e.g., COOK_BLEND_SIMPLE_01, COOK_BOIL_SIMPLE_01, COOK_REDUCE_HEAT_01) - they have no variables and work perfectly!
- If no simple step matches, use steps with variables and fill them ALL with CONCRETE values (not placeholders!)
- For {{{{ingredient}}}}: extract ingredient name WITH CORRECT ARTICLE and CASE (e.g., ""die Hähnchenbrust"", ""den Spargel"", ""dem Topf"")
- For {{{{action}}}}: use IMPERATIVE VERB FORM (e.g., ""dünste"" not ""dünsten"", ""schneide"" not ""schneiden"")
- For {{{{shape}}}}: extract or infer cut shape (e.g., ""Würfel"", ""cubes"", ""strips"")
- For {{{{duration}}}}: extract time (e.g., ""5 Minuten"", ""5 minutes"", ""10 Min"")
- For {{{{temp}}}}: extract temperature (e.g., ""180°C"", ""medium heat"", ""mittlere Hitze"")
- **CRITICAL: Check grammar** - correct articles (der/die/das/den/dem), verb forms (imperative), and avoid redundancy
- For soups/sauces: Use COOK_BLEND_SIMPLE_01 for blending/pureeing
- If the user's text is vague, make reasonable assumptions for a typical home cook
- Steps must be in {languageName}
- Preserve the user's original intent and order as much as possible
- If translation is needed, provide outputs in {languageName}

**Available SmartSteps by Phase:**
{stepsJson}

**Output Format:**
Return ONLY valid JSON matching the schema (enforced). The 'reasoning' field should briefly explain how you interpreted the user's instructions.
";
        }

        private async Task<GeneratedStepsResult> CallOpenAiAsync(
            string apiKey,
            string prompt,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("🤖 Calling OpenAI API - Model: {Model}, Prompt length: {Length} chars", DefaultModel, prompt.Length);


            var requestBody = new
            {
                model = DefaultModel,
                messages = new[]
                {
                    new { role = "system", content = "You are a professional recipe step generator. Always respond with valid JSON matching the provided schema." },
                    new { role = "user", content = prompt }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "recipe_steps_generation",
                        strict = false,
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                steps = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            master_step_id = new { type = "string" },
                                            step_order = new { type = "number" },
                                            variables = new
                                            {
                                                type = "object",
                                                additionalProperties = true
                                            },
                                            preview_text = new { type = "string" }
                                        },
                                        required = new[] { "master_step_id", "step_order", "variables", "preview_text" }
                                    }
                                },
                                reasoning = new { type = "string" }
                            },
                            required = new[] { "steps", "reasoning" }
                        }
                    }
                },
                temperature = 0.7,
                max_tokens = 2000
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

            _logger.LogInformation("✅ OpenAI Response - Status: {Status}, Body length: {Length} chars", response.StatusCode, responseBody?.Length ?? 0);


            if (!response.IsSuccessStatusCode)
            {
                // Log the full body for diagnostics, but never surface it to the caller/client
                // (it can contain API/config details).
                _logger.LogError("OpenAI API error: {Status} - {Body}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
            }

            var aiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseBody);
            var content = aiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("OpenAI returned empty response");
            }

            var result = JsonSerializer.Deserialize<AiGeneratedSteps>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            // Post-process: Clean preview text by removing optional segments
            var cleanedSteps = result?.Steps?.Select(s =>
            {
                var cleanedPreview = CleanOptionalSegments(s.PreviewText, s.Variables ?? new Dictionary<string, string>());
                return new GeneratedStep
                {
                    MasterStepId = s.MasterStepId,
                    StepOrder = s.StepOrder,
                    Variables = s.Variables ?? new Dictionary<string, string>(),
                    PreviewText = cleanedPreview
                };
            }).ToList() ?? new List<GeneratedStep>();

            return new GeneratedStepsResult
            {
                Success = true,
                Steps = cleanedSteps,
                Reasoning = result?.Reasoning ?? ""
            };
        }

        private string CleanOptionalSegments(string text, Dictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Remove optional segments [...] and (...)
            // Pattern: matches [...] or (...) with or without {{variables}}
            var optionalPattern = new System.Text.RegularExpressions.Regex(@"\[([^\[\]]*)\]|\(([^()]*)\)");

            string previous = null;
            while (text != previous)
            {
                previous = text;
                text = optionalPattern.Replace(text, match =>
                {
                    var inner = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                    // Check if segment contains variables {{varName}}
                    var varPattern = new System.Text.RegularExpressions.Regex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}");
                    var varMatches = varPattern.Matches(inner);

                    if (varMatches.Count == 0)
                    {
                        // No variables → remove entire segment (it's optional text)
                        return "";
                    }

                    // Check if all variables have values
                    bool allHaveValues = true;
                    foreach (System.Text.RegularExpressions.Match varMatch in varMatches)
                    {
                        var varName = varMatch.Groups[1].Value;
                        if (!variables.ContainsKey(varName) || string.IsNullOrWhiteSpace(variables[varName]))
                        {
                            allHaveValues = false;
                            break;
                        }
                    }

                    // If all variables have values → keep inner content (without brackets)
                    // Otherwise → remove entire segment
                    return allHaveValues ? inner : "";
                });
            }

            // Remove fallback syntax {~text~varName~}
            var fallbackPattern = new System.Text.RegularExpressions.Regex(@"\{~([^~]+)~([^~]+)~\}");
            text = fallbackPattern.Replace(text, match =>
            {
                var fallbackText = match.Groups[1].Value;
                var varNames = match.Groups[2].Value;

                bool hasAnyValue = varNames.Split('|')
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Any(v => variables.ContainsKey(v) && !string.IsNullOrWhiteSpace(variables[v]));

                return hasAnyValue ? "" : fallbackText;
            });

            return text;
        }

        private string NormalizeLanguageKey(string language)
        {
            return (language?.ToLower()) switch
            {
                "es" => "esp",
                "pt" => "prt",
                "se" => "sv",
                "dk" => "da",
                _ => language?.ToLower() ?? "de"
            };
        }

        private string GetLanguageName(string language)
        {
            return language?.ToLower() switch
            {
                "de" => "German",
                "en" => "English",
                "es" or "esp" => "Spanish",
                "pt" or "prt" => "Portuguese",
                "id" => "Indonesian",
                "nl" => "Dutch",
                "sv" => "Swedish",
                "da" => "Danish",
                "no" => "Norwegian",
                "ms" => "Malay",
                _ => "German"
            };
        }

        private string GetPhaseName(int phase, string language)
        {
            var phases = new Dictionary<int, Dictionary<string, string>>
            {
                [0] = new() { ["de"] = "Basis", ["en"] = "Base" },
                [1] = new() { ["de"] = "Vorbereitung", ["en"] = "Preparation" },
                [2] = new() { ["de"] = "Kochen", ["en"] = "Cooking" },
                [3] = new() { ["de"] = "Würzen/Finishing", ["en"] = "Seasoning/Finishing" },
                [4] = new() { ["de"] = "Anrichten/Servieren", ["en"] = "Plating/Serving" }
            };

            return phases.TryGetValue(phase, out var names) && names.TryGetValue(language, out var name)
                ? name
                : $"Phase {phase}";
        }

        // DTOs
        private class MasterStepsDocument
        {
            [JsonPropertyName("master_steps")]
            public List<MasterStepData>? MasterSteps { get; set; }
        }

        private class MasterStepData
        {
            [JsonPropertyName("master_id")]
            public string MasterId { get; set; } = "";

            [JsonPropertyName("phase")]
            public int Phase { get; set; }

            [JsonPropertyName("action")]
            public string? Action { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("template")]
            public string? Template { get; set; }

            [JsonPropertyName("variables")]
            public List<string>? Variables { get; set; }

            [JsonPropertyName("required_variables")]
            public List<string>? RequiredVariables { get; set; }
        }

        private class SmartStepInfo
        {
            public string MasterId { get; set; } = "";
            public int Phase { get; set; }
            public string? Action { get; set; }
            public string? Description { get; set; }
            public string? Template { get; set; }
            public List<string> Variables { get; set; } = new();
            public List<string> RequiredVariables { get; set; } = new();
        }

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

        private class AiGeneratedSteps
        {
            [JsonPropertyName("steps")]
            public List<AiStep>? Steps { get; set; }

            [JsonPropertyName("reasoning")]
            public string? Reasoning { get; set; }
        }

        private class AiStep
        {
            [JsonPropertyName("master_step_id")]
            public string MasterStepId { get; set; } = "";

            [JsonPropertyName("step_order")]
            public int StepOrder { get; set; }

            [JsonPropertyName("variables")]
            public Dictionary<string, string>? Variables { get; set; }

            [JsonPropertyName("preview_text")]
            public string PreviewText { get; set; } = "";
        }

        // ===== NEW AI-GENERATED CONCRETE STEPS SYSTEM =====

        /// <summary>
        /// Generiert konkrete Steps mit Übersetzungen (NEUES SYSTEM - kein Master Step Matching)
        /// </summary>
        public async Task<ConcreteStepsResult> GenerateConcreteStepsAsync(
            string recipeTitle,
            string category,
            List<(int id, string name)> ingredients,
            string? instructionsText = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("🤖 Generating concrete AI steps with translations - Recipe: '{Title}'", recipeTitle);

            try
            {
                var apiKey = _config["OpenAI:ApiKey"] ?? _config["SecretKeyOpenAi"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("OpenAI API key is missing");
                }

                var prompt = BuildConcreteStepsPrompt(recipeTitle, category, ingredients, instructionsText);

                var response = await CallOpenAiForConcreteStepsAsync(apiKey, prompt, cancellationToken);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate concrete steps for recipe: {Title}", recipeTitle);
                return new ConcreteStepsResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string BuildConcreteStepsPrompt(
            string recipeTitle,
            string category,
            List<(int id, string name)> ingredients,
            string? instructionsText)
        {
            var ingredientList = string.Join("\n", ingredients.Select((ing, i) => $"{i + 1}. {ing.name} (ID: {ing.id})"));

            var taskDescription = string.IsNullOrWhiteSpace(instructionsText)
                ? $@"Generate typical cooking steps for a {category} recipe titled ""{recipeTitle}""."
                : $@"Convert the user's cooking instructions into structured steps.

**User's Instructions:**
{instructionsText}";

            return $@"You are a professional recipe assistant. Generate concrete, editable cooking steps.

**Recipe Information:**
- Title: {recipeTitle}
- Category: {category}
- Ingredients:
{ingredientList}

{taskDescription}

**Your Task:**
1. Generate 4-15 concrete cooking steps (NO templates, NO variables!)
2. Write SPECIFIC steps using the EXACT ingredients (e.g., ""Schneide die Tomaten in Würfel"", NOT ""Schneide {{{{ingredient}}}} in {{{{shape}}}}"")
3. Generate steps in GERMAN only (user will edit them, then we translate at the end)
4. Assign each step to a phase: 0=Basis, 1=Preparation, 2=Cooking, 3=Seasoning/Finishing, 4=Serving
5. If a step relates to a specific ingredient, include its ingredient_id

**Guidelines:**
- Steps must be CONCRETE and SPECIFIC (""Schneide die Tomaten in kleine Würfel"" ✓, ""Schneide {{{{ingredient}}}} in {{{{shape}}}}"" ✗)
- Use IMPERATIVE form (German: ""Schneide"", NOT ""Schneiden"")
- Ensure CORRECT GRAMMAR (articles, verb forms, etc.)
- Keep steps simple and practical for home cooks
- Order steps logically (prep → cook → season → serve)

**Example Output:**
{{
  ""steps"": [
    {{
      ""step_order"": 1,
      ""phase"": 1,
      ""ingredient_id"": 123,
      ""text"": ""Schneide die Tomaten in kleine Würfel.""
    }},
    {{
      ""step_order"": 2,
      ""phase"": 2,
      ""ingredient_id"": null,
      ""text"": ""Erhitze etwas Olivenöl in einer Pfanne.""
    }}
  ],
  ""reasoning"": ""Brief explanation of your approach""
}}

**Output Format:**
Return ONLY valid JSON matching the schema (enforced).
";
        }

        private async Task<ConcreteStepsResult> CallOpenAiForConcreteStepsAsync(
            string apiKey,
            string prompt,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("🤖 Calling OpenAI API for concrete steps - Prompt length: {Length} chars", prompt.Length);


            var requestBody = new
            {
                model = DefaultModel,
                messages = new[]
                {
                    new { role = "system", content = "You are a professional recipe step generator. Generate concrete, reusable cooking steps with accurate translations in all languages." },
                    new { role = "user", content = prompt }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "concrete_recipe_steps",
                        strict = false,
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                steps = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            step_order = new { type = "number" },
                                            phase = new { type = "number" },
                                            ingredient_id = new { type = "number" },
                                            text = new { type = "string" }
                                        },
                                        required = new[] { "step_order", "phase", "text" }
                                    }
                                },
                                reasoning = new { type = "string" }
                            },
                            required = new[] { "steps", "reasoning" }
                        }
                    }
                },
                temperature = 0.7,
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

            _logger.LogInformation("✅ OpenAI Response - Status: {Status}, Body length: {Length} chars", response.StatusCode, responseBody?.Length ?? 0);


            if (!response.IsSuccessStatusCode)
            {
                // Log the full body for diagnostics, but never surface it to the caller/client
                // (it can contain API/config details).
                _logger.LogError("OpenAI API error: {Status} - {Body}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
            }

            var aiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseBody);
            var content = aiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("OpenAI returned empty response");
            }

            var result = JsonSerializer.Deserialize<AiConcreteStepsResponse>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return new ConcreteStepsResult
            {
                Success = true,
                Steps = result?.Steps ?? new List<ConcreteStep>(),
                Reasoning = result?.Reasoning ?? ""
            };
        }

        // DTOs for Concrete Steps System

        private class AiConcreteStepsResponse
        {
            [JsonPropertyName("steps")]
            public List<ConcreteStep>? Steps { get; set; }

            [JsonPropertyName("reasoning")]
            public string? Reasoning { get; set; }
        }

        public class ConcreteStepsResult
        {
            public bool Success { get; set; }
            public List<ConcreteStep> Steps { get; set; } = new();
            public string Reasoning { get; set; } = "";
            public string? ErrorMessage { get; set; }
        }

        public class ConcreteStep
        {
            [JsonPropertyName("step_order")]
            public int StepOrder { get; set; }

            [JsonPropertyName("phase")]
            public int Phase { get; set; }

            [JsonPropertyName("ingredient_id")]
            public int? IngredientId { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; } = "";
        }

        /// <summary>
        /// Übersetzt mehrere Steps auf einmal in alle 10 Sprachen (beim Speichern des Rezepts)
        /// </summary>
        public async Task<TranslationResult> TranslateStepsAsync(
            List<(int stepOrder, string germanText)> steps,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("🌍 Translating {Count} steps into 10 languages", steps.Count);

            try
            {
                var apiKey = _config["OpenAI:ApiKey"] ?? _config["SecretKeyOpenAi"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("OpenAI API key is missing");
                }

                var stepsJson = JsonSerializer.Serialize(steps.Select(s => new { order = s.stepOrder, de = s.germanText }));

                var prompt = $@"You are a professional translator for cooking recipes. Translate the following German cooking steps into 9 other languages.

**Steps to translate:**
{stepsJson}

**Target languages:** English (en), Spanish (esp), Portuguese (prt), Indonesian (id), Dutch (nl), Swedish (sv), Danish (da), Norwegian (no), Malay (ms)

**Guidelines:**
- Maintain the IMPERATIVE verb form in each language
- Use CORRECT GRAMMAR and natural phrasing for each language
- Keep the meaning EXACT (don't add or remove information)
- Use proper cooking terminology in each language
- Ensure translations are CONTEXT-AWARE (consider the cooking context)

**Output Format:**
Return JSON array with translations for each step:
{{
  ""translations"": [
    {{
      ""order"": 1,
      ""de"": ""Original German text"",
      ""en"": ""English translation"",
      ""esp"": ""Spanish translation"",
      ""prt"": ""Portuguese translation"",
      ""id"": ""Indonesian translation"",
      ""nl"": ""Dutch translation"",
      ""sv"": ""Swedish translation"",
      ""da"": ""Danish translation"",
      ""no"": ""Norwegian translation"",
      ""ms"": ""Malay translation""
    }}
  ]
}}
";

                var requestBody = new
                {
                    model = DefaultModel,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional cooking recipe translator. Provide accurate, natural translations while maintaining cooking terminology." },
                        new { role = "user", content = prompt }
                    },
                    response_format = new { type = "json_object" },
                    temperature = 0.3, // Lower temperature for more consistent translations
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

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ OpenAI Translation API error: {Status} - {Body}", response.StatusCode, responseBody);
                    throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
                }

                var aiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseBody);
                var content = aiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("OpenAI returned empty response");
                }

                var result = JsonSerializer.Deserialize<TranslationApiResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return new TranslationResult
                {
                    Success = true,
                    Translations = result?.Translations ?? new List<StepTranslation>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate steps");
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private class TranslationApiResponse
        {
            [JsonPropertyName("translations")]
            public List<StepTranslation>? Translations { get; set; }
        }

        public class TranslationResult
        {
            public bool Success { get; set; }
            public List<StepTranslation> Translations { get; set; } = new();
            public string? ErrorMessage { get; set; }
        }

        public class StepTranslation
        {
            [JsonPropertyName("order")]
            public int Order { get; set; }

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
}
