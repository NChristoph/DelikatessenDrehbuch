using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    /// <summary>
    /// Übersetzung eines Rezept-Steps in eine andere Sprache
    /// </summary>
    public class RecipeStepTranslation
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Referenz auf das Rezept (für schnelle Queries)
        /// </summary>
        [Required]
        public int RecipeId { get; set; }

        /// <summary>
        /// Referenz auf den Step
        /// </summary>
        [Required]
        public int StepId { get; set; }

        /// <summary>
        /// Sprach-Code (de, en, esp, prt, id, nl, sv, da, no, ms)
        /// </summary>
        [Required]
        [MaxLength(5)]
        public string Language { get; set; } = "";

        /// <summary>
        /// Übersetzter Text
        /// </summary>
        [Required]
        public string TranslatedText { get; set; } = "";

        /// <summary>
        /// Wann wurde übersetzt
        /// </summary>
        public DateTime TranslatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Welcher Provider (gpt-4o, gpt-4o-mini, deepl, etc.)
        /// </summary>
        [MaxLength(50)]
        public string TranslationProvider { get; set; } = "gpt-4o";

        /// <summary>
        /// true = Hauptsprache (sofort übersetzt), false = On-Demand
        /// </summary>
        public bool IsCoreSprache { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(RecipeId))]
        public RecipeBaseData? Recipe { get; set; }

        [ForeignKey(nameof(StepId))]
        public RecipeStep? Step { get; set; }
    }
}
