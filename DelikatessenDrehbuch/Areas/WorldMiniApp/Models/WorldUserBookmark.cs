using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserBookmark
    {
        public int Id { get; set; }
        public RecipeBaseData Recipe { get; set; }
        public WorldAppUser WorldAppUser { get; set; }
    }
}
