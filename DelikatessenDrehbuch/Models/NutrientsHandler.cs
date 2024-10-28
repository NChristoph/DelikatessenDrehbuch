namespace DelikatessenDrehbuch.Models
{
    public class NutrientsHandler
    {
        public int Id { get; set; } 
        public IngredientNutrientHandler IngredientNutrientHandler { get; set; }
        public Nutrient Nutrient { get; set; }
        public Quantity Quantity { get; set; }
        public Measure Measure { get; set; }
    }
}
