namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class Keyword
    {
        public int Id { get; set; }
        public string Word { get; set; }

        public ICollection<RecipeBaseKeyword> RecipeLinks { get; set; }
    }
}
