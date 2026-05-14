using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    /// <summary>
    /// Zubereitungsschritte (normalisiert: 1 Row pro Sprache)
    /// AI-übersetzt beim Speichern
    /// </summary>
    [Table("RecipeSteps")]
    public class RecipeSteps
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign Key zu RecipeBaseData
        /// </summary>
        public int RecipeId { get; set; }

        /// <summary>
        /// Sprachcode: de, en, esp, prt, id, nl, sv, da, no, ms
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string Culture { get; set; }

        /// <summary>
        /// Zubereitungsschritte als Text
        /// </summary>
        [Required]
        public string Text { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Property
        [ForeignKey("RecipeId")]
        public virtual RecipeBaseData? Recipe { get; set; }
    }
}
