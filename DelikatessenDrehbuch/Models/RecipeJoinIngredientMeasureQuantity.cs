namespace DelikatessenDrehbuch.Models
{
    public class RecipeJoinIngredientMeasureQuantity
    {
        public int Id { get; set; }
        public RecipeBaseData Recipe { get; set; }
        public IngredientMeasureQuantity Ingredient { get; set; }
    }
}
