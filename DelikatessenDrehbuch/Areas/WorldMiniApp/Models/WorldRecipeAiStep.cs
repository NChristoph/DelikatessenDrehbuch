using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// AI-generierte Rezeptschritte mit Übersetzungen in allen Sprachen.
    /// Diese Steps sind konkret (keine Templates/Variablen) und werden von OpenAI
    /// direkt mit allen Übersetzungen generiert.
    /// </summary>
    [Table("WorldRecipeAiSteps")]
    public class WorldRecipeAiStep
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Referenz zum Rezept (RecipeBaseData)
        /// </summary>
        public int RecipeId { get; set; }

        /// <summary>
        /// Reihenfolge des Steps im Rezept (1, 2, 3, ...)
        /// </summary>
        public int StepOrder { get; set; }

        /// <summary>
        /// Phase: 0=Basis, 1=Vorbereitung, 2=Kochen, 3=Würzen/Finishing, 4=Anrichten/Servieren
        /// </summary>
        public int Phase { get; set; }

        // ===== Übersetzungen (AI-generiert) =====

        [MaxLength(500)]
        public string TextDe { get; set; } = "";

        [MaxLength(500)]
        public string TextEn { get; set; } = "";

        [MaxLength(500)]
        public string TextEsp { get; set; } = "";

        [MaxLength(500)]
        public string TextPrt { get; set; } = "";

        [MaxLength(500)]
        public string TextId { get; set; } = "";

        [MaxLength(500)]
        public string TextNl { get; set; } = "";

        [MaxLength(500)]
        public string TextSv { get; set; } = "";

        [MaxLength(500)]
        public string TextDa { get; set; } = "";

        [MaxLength(500)]
        public string TextNo { get; set; } = "";

        [MaxLength(500)]
        public string TextMs { get; set; } = "";

        /// <summary>
        /// Optional: Zutat-ID, falls dieser Step sich auf eine spezifische Zutat bezieht
        /// (z.B. "Schneide Tomaten in Würfel" → IngredientId = Tomaten-ID)
        /// </summary>
        public int? IngredientId { get; set; }

        /// <summary>
        /// Rezept-Kategorie (z.B. "Pasta", "Soup", "Dessert") für Smart Suggestions
        /// </summary>
        [MaxLength(50)]
        public string? RecipeCategory { get; set; }

        /// <summary>
        /// Wie oft wurde dieser Step verwendet? (für Popularität/Suggestions)
        /// Wird inkrementiert, wenn ähnliche Steps in anderen Rezepten verwendet werden
        /// </summary>
        public int UsageCount { get; set; } = 1;

        /// <summary>
        /// Erstellungsdatum
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("RecipeId")]
        public virtual RecipeBaseData? Recipe { get; set; }

        /// <summary>
        /// Gibt den Text in der angegebenen Sprache zurück
        /// </summary>
        public string GetText(string language)
        {
            return language?.ToLower() switch
            {
                "de" => TextDe,
                "en" => TextEn,
                "es" or "esp" => TextEsp,
                "pt" or "prt" => TextPrt,
                "id" => TextId,
                "nl" => TextNl,
                "sv" or "se" => TextSv,
                "da" or "dk" => TextDa,
                "no" => TextNo,
                "ms" => TextMs,
                _ => TextDe
            };
        }
    }
}
