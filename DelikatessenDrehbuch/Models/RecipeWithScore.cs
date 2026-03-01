namespace DelikatessenDrehbuch.Models
{
    public class RecipeWithScore
    {
        public Recipes Recipe { get; set; }
        public int MatchingIngredients { get; set; }
        public int TotalIngredients { get; set; }
        public int ScorePercent => TotalIngredients > 0
            ? (int)((double)MatchingIngredients / TotalIngredients * 100)
            : 0;
    }
}
