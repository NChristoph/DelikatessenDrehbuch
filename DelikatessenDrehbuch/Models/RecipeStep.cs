using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    /// <summary>
    /// Neues System: Einfache Step-Struktur mit Übersetzungen
    /// </summary>
    public class RecipeStep
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Referenz auf das Rezept
        /// </summary>
        [Required]
        public int RecipeId { get; set; }

        /// <summary>
        /// Reihenfolge (1, 2, 3, ...)
        /// </summary>
        [Required]
        public int StepOrder { get; set; }

        /// <summary>
        /// Step-Text in der Original-Sprache des Creators
        /// </summary>
        [Required]
        public string StepText { get; set; } = "";

        /// <summary>
        /// Sprache in der dieser Step erstellt wurde (de, en, esp, prt)
        /// </summary>
        [MaxLength(5)]
        public string SourceLanguage { get; set; } = "de";

        /// <summary>
        /// Wann wurde der Step erstellt
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey(nameof(RecipeId))]
        public RecipeBaseData? Recipe { get; set; }

        public virtual ICollection<RecipeStepTranslation> Translations { get; set; } = new List<RecipeStepTranslation>();
    }
}
