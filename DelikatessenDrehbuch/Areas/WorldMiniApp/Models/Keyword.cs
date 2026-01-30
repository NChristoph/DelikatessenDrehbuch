namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class Keyword
    {
        public int Id { get; set; }
        public string Word_DE { get; set; }
        public string Word_EN { get; set; }
        public string Word_ESP { get; set; }
        public string Word_PRT { get; set; }

        public ICollection<RecipeBaseKeyword> RecipeLinks { get; set; }
    }
}
