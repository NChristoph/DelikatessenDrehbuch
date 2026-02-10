using DelikatessenDrehbuch.Models;
using System.ComponentModel.DataAnnotations;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class EditPostingRecipeViewModel
    {
        public int PostingId { get; set; }
        public int RecipeId { get; set; }

        [Required]
        public string Title { get; set; }
        public string Category { get; set; }
        public string Preferences { get; set; }
        public int PersonCount { get; set; }
        public int PreperationTime { get; set; }

        public string? CurrentImageUrl { get; set; }
        public IFormFile? NewContent { get; set; }

        public List<EditPostingIngredientRowViewModel> Ingredients { get; set; } = new();

        public List<IngredientsAndNutrients> AvailableIngredients { get; set; } = new();
        public List<Measure> AvailableMeasures { get; set; } = new();
    }

    public class EditPostingIngredientRowViewModel
    {
        public int IngredientId { get; set; }
        public int MeasureId { get; set; }
        public double Quantity { get; set; }
    }
}
