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
        public int PreparationTime { get; set; }

        public string? CurrentImageUrl { get; set; }
        public IFormFile? NewContent { get; set; }

        public List<EditPostingIngredientRowViewModel> Ingredients { get; set; } = new();
        public List<EditPostingStepRowViewModel> Steps { get; set; } = new();
        public List<int> SelectedKeywordIds { get; set; } = new();

        public List<IngredientsAndNutrients> AvailableIngredients { get; set; } = new();
        public List<Measure> AvailableMeasures { get; set; } = new();
        public List<RecipePreparationSteps> AvailableSteps { get; set; } = new();
        public List<Keyword> AvailableKeywords { get; set; } = new();
        public List<JoinIngredientPreparationStep> IngredientStepJoins { get; set; } = new();
        public List<EditPostingSmartStepViewModel> ExistingSmartSteps { get; set; } = new();
    }

    public class EditPostingSmartStepViewModel
    {
        public string MasterStepKey { get; set; } = string.Empty;
        public string VariablesJson { get; set; } = "{}";
        public int StepIndex { get; set; }
    }

    public class EditPostingIngredientRowViewModel
    {
        public int IngredientId { get; set; }
        public int MeasureId { get; set; }
        public double Quantity { get; set; }
    }

    public class EditPostingStepRowViewModel
    {
        public int PreparationStepId { get; set; }
        public int StepIndex { get; set; }
    }
}
