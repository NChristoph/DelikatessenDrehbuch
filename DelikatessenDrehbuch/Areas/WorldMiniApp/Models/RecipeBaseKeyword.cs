using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class RecipeBaseKeyword
    {
        public int RecipeBaseDataId { get; set; }
        public RecipeBaseData RecipeBaseData { get; set; }

        public int KeywordId { get; set; }
        public Keyword Keyword { get; set; }
    }
}
