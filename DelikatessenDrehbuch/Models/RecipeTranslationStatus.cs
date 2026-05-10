using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    /// <summary>
    /// Status welche Sprachen für ein Rezept verfügbar sind
    /// </summary>
    public class RecipeTranslationStatus
    {
        [Key]
        public int RecipeId { get; set; }

        // 4 Hauptsprachen (sofort übersetzt nach Upload)
        public bool HasDE { get; set; }
        public bool HasEN { get; set; }
        public bool HasESP { get; set; }
        public bool HasPRT { get; set; }

        // 6 On-Demand Sprachen (nur bei Bedarf)
        public bool HasID { get; set; }
        public bool HasNL { get; set; }
        public bool HasSV { get; set; }
        public bool HasDA { get; set; }
        public bool HasNO { get; set; }
        public bool HasMS { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation Property
        [ForeignKey(nameof(RecipeId))]
        public RecipeBaseData? Recipe { get; set; }
    }
}
