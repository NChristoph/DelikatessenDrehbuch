using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Models
{
    public class RecipeBaseData
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int PersonCount { get; set; }
        public string Category { get; set; }
        public string Preferences { get; set; }
        public int PreperationTime { get; set; }
        public int LikeCount { get; set; }


        public virtual ICollection<RecipeJoinIngredientMeasureQuantity> Ingredients { get; set; }
        public virtual ICollection<RecipeJoyinPreperationSteps> Steps { get; set; }
        public virtual ICollection<RecipeBaseDataImage> Images { get; set; }
        public virtual ICollection<RecipeBaseKeyword> RecipeKeywords { get; set; }
        
    }
}
