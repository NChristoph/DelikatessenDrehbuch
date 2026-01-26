using DelikatessenDrehbuch.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserPosting
    {
        public int Id { get; set; }
        public string CreatorId { get; set; }
        public string CreatorName { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        
        public DateTime CreationTime { get; set; } = DateTime.Now;

        [NotMapped]
        public List<Measure> Measure { get; set; }
        public RecipeBaseData Recipe { get; set; }
        [NotMapped]
        public List<IngredientsAndNutrients> ToSelectIngredientsAndNutrients { get; set; }
        [NotMapped]
        public List<RecipePreperationSteps> ToSelectRecipePreperationSteps { get; set; }

        [NotMapped]
        public List<IngredientMeasureQuantity> IngredientMeasureQuantity { get; set; }
        [NotMapped]
        public List<RecipeJoyinPreperationSteps> RecipePreperationSteps { get; set; }

        [NotMapped]
        public IFormFile Content { get; set; }
    }
}
