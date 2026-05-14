using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using System.ComponentModel.DataAnnotations;

namespace DelikatessenDrehbuch.Models
{
    public class RecipeBaseData
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Range(1, 100)]
        public int PersonCount { get; set; }

        [MaxLength(100)]
        public string Category { get; set; }

        [MaxLength(200)]
        public string Preferences { get; set; }

        [Range(0, 999)]
        public int PreparationTime { get; set; }

        public int LikeCount { get; set; }

        // Navigation Properties
        public virtual ICollection<RecipeJoinIngredientMeasureQuantity> Ingredients { get; set; }

        // NEW: Zubereitungsschritte (AI-übersetzt, eigene Tabelle, normalisiert pro Sprache)
        public virtual ICollection<RecipeSteps>? Steps { get; set; }

        public virtual ICollection<RecipeBaseDataImage> Images { get; set; }
        public virtual ICollection<RecipeBaseKeyword> RecipeKeywords { get; set; }

    }
}
