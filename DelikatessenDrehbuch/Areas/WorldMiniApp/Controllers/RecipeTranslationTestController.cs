using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    [Route("api/translation-test")]
    public class RecipeTranslationTestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IRecipeStepTranslationService _translationService;
        private readonly ILogger<RecipeTranslationTestController> _logger;

        public RecipeTranslationTestController(
            ApplicationDbContext db,
            IRecipeStepTranslationService translationService,
            ILogger<RecipeTranslationTestController> logger)
        {
            _db = db;
            _translationService = translationService;
            _logger = logger;
        }

        /// <summary>
        /// TEST 1: Erstelle Test-Rezept mit Steps
        /// GET /api/translation-test/create-test-recipe
        /// </summary>
        [HttpGet("create-test-recipe")]
        public async Task<IActionResult> CreateTestRecipe()
        {
            try
            {
                // 1. Erstelle Test-Rezept
                var recipe = new RecipeBaseData
                {
                    Title = "Schweinebraten Test-Rezept",
                    Category = "Main",
                    PersonCount = 4,
                    PreparationTime = 180,
                    Preferences = "Pork"
                };

                _db.RecipeBaseData.Add(recipe);
                await _db.SaveChangesAsync();

                // 2. Füge Test-Steps hinzu (aus deinem Beispiel)
                var steps = new[]
                {
                    "Heize ♨️ Backofen auf 180°C Ober-/Unterhitze vor.",
                    "Schneide die Schweineschulter rautenförmig ein.",
                    "Schäle die Knoblauchzehe, die Karotten, die Knollensellerie und den Zwiebeln.",
                    "Schneide den Lauch in grobe Scheiben.",
                    "Schneide die Zwiebeln, die Karotten und die Knollensellerie in grobe Würfel.",
                    "Hacke die Knoblauchzehe mit einem Messer sehr fein.",
                    "Reibe der Schweineschulter gleichmäßig mit den Salz, den Pfeffer und den Kümmel (gemahlen) ein.",
                    "Erhitze das Butterschmalz in den 🍲 Bräter auf höchster Stufe und brate die Schweineschulter scharf an.",
                    "Verteile die geschnittene Knollensellerie, die gehackte Knoblauchzehe, die geschnittene Karotten, die geschnittene Zwiebeln und den geschnittenen Lauch gleichmäßig in den 🍲 Bräter um die Schweineschulter herum.",
                    "Lösche mit den Dunkelbier und der Rinderbrühe ab und kratze den Bratensatz vom Boden von den 🍲 Bräter.",
                    "Schiebe den 🍲 Bräter in den auf 180 C vorgeheizten Backofen und gare 100-120 Minuten."
                };

                for (int i = 0; i < steps.Length; i++)
                {
                    var step = new RecipeStep
                    {
                        RecipeId = recipe.Id,
                        StepOrder = i + 1,
                        StepText = steps[i],
                        SourceLanguage = "de",
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.RecipeSteps.Add(step);
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Test recipe created with ID: {RecipeId}", recipe.Id);

                return Ok(new
                {
                    success = true,
                    recipeId = recipe.Id,
                    message = $"Test-Rezept erstellt mit {steps.Length} Schritten",
                    steps = steps.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create test recipe");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// TEST 2: Starte Übersetzung (Background)
        /// POST /api/translation-test/translate/{recipeId}
        /// </summary>
        [HttpPost("translate/{recipeId}")]
        public async Task<IActionResult> TranslateRecipe(int recipeId)
        {
            try
            {
                var recipe = await _db.RecipeBaseData.FindAsync(recipeId);
                if (recipe == null)
                {
                    return NotFound(new { success = false, error = "Recipe not found" });
                }

                _logger.LogInformation("🚀 Starting translation for recipe {RecipeId}", recipeId);

                // Queue Background Translation
                await _translationService.QueueTranslationAsync(recipeId);

                return Ok(new
                {
                    success = true,
                    message = "Übersetzung gestartet (läuft im Hintergrund)",
                    recipeId = recipeId,
                    info = "Warte ca. 30-60 Sekunden und rufe dann /api/translation-test/status/{recipeId} auf"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start translation");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// TEST 3: Prüfe Status der Übersetzung
        /// GET /api/translation-test/status/{recipeId}
        /// </summary>
        [HttpGet("status/{recipeId}")]
        public async Task<IActionResult> GetTranslationStatus(int recipeId)
        {
            try
            {
                var status = await _db.RecipeTranslationStatuses
                    .FirstOrDefaultAsync(s => s.RecipeId == recipeId);

                if (status == null)
                {
                    return Ok(new
                    {
                        success = true,
                        recipeId = recipeId,
                        status = "not_translated",
                        message = "Noch keine Übersetzungen vorhanden"
                    });
                }

                var availableLanguages = new List<string>();
                if (status.HasDE) availableLanguages.Add("DE");
                if (status.HasEN) availableLanguages.Add("EN");
                if (status.HasESP) availableLanguages.Add("ESP");
                if (status.HasPRT) availableLanguages.Add("PRT");
                if (status.HasID) availableLanguages.Add("ID");
                if (status.HasNL) availableLanguages.Add("NL");
                if (status.HasSV) availableLanguages.Add("SV");
                if (status.HasDA) availableLanguages.Add("DA");
                if (status.HasNO) availableLanguages.Add("NO");
                if (status.HasMS) availableLanguages.Add("MS");

                var translationCount = await _db.RecipeStepTranslations
                    .Where(t => t.RecipeId == recipeId)
                    .GroupBy(t => t.Language)
                    .Select(g => new { Language = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    recipeId = recipeId,
                    availableLanguages = availableLanguages,
                    lastUpdated = status.LastUpdated,
                    translationDetails = translationCount,
                    coreLanguagesReady = status.HasEN && status.HasESP && status.HasPRT
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get status");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// TEST 4: Lade Steps in bestimmter Sprache
        /// GET /api/translation-test/steps/{recipeId}/{language}
        /// </summary>
        [HttpGet("steps/{recipeId}/{language}")]
        public async Task<IActionResult> GetStepsInLanguage(int recipeId, string language)
        {
            try
            {
                var steps = await _translationService.GetStepsForLanguageAsync(recipeId, language);

                if (steps.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = $"Keine Steps in Sprache '{language}' gefunden"
                    });
                }

                var formatted = steps.Select((text, index) => new
                {
                    stepNumber = index + 1,
                    text = text
                });

                return Ok(new
                {
                    success = true,
                    recipeId = recipeId,
                    language = language,
                    stepCount = steps.Count,
                    steps = formatted
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get steps");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// TEST 5: On-Demand Übersetzung (z.B. Indonesisch)
        /// POST /api/translation-test/translate-ondemand/{recipeId}/{language}
        /// </summary>
        [HttpPost("translate-ondemand/{recipeId}/{language}")]
        public async Task<IActionResult> TranslateOnDemand(int recipeId, string language)
        {
            try
            {
                _logger.LogInformation("🚀 On-demand translation for recipe {RecipeId} to {Lang}", recipeId, language);

                var steps = await _translationService.TranslateStepsOnDemandAsync(recipeId, language);

                if (steps.Count == 0)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        error = "Translation failed or returned empty"
                    });
                }

                var formatted = steps.Select((text, index) => new
                {
                    stepNumber = index + 1,
                    text = text
                });

                return Ok(new
                {
                    success = true,
                    recipeId = recipeId,
                    language = language,
                    stepCount = steps.Count,
                    steps = formatted,
                    message = "On-Demand Übersetzung abgeschlossen"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed on-demand translation");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// TEST 6: Alle Test-Rezepte auflisten
        /// GET /api/translation-test/list
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> ListTestRecipes()
        {
            try
            {
                var recipes = await _db.RecipeBaseData
                    .Where(r => r.Title.Contains("Test"))
                    .OrderByDescending(r => r.Id)
                    .Take(10)
                    .Select(r => new
                    {
                        r.Id,
                        r.Title,
                        StepCount = _db.RecipeSteps.Count(s => s.RecipeId == r.Id),
                        TranslationCount = _db.RecipeStepTranslations
                            .Where(t => t.RecipeId == r.Id)
                            .Select(t => t.Language)
                            .Distinct()
                            .Count()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = recipes.Count,
                    recipes = recipes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list recipes");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
