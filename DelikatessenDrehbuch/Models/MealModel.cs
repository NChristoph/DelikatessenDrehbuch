namespace DelikatessenDrehbuch.Models
{
    public class MealModel
    {
        public MealPlan MealPlan { get; set; }
        public List<Recipes> Recipes { get; set; }
        
        public List<IngredientHandlerModel> Ingredients { get; set;}


    }
}
