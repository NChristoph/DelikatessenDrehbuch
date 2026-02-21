namespace DelikatessenDrehbuch.Models
{
    public class IngredientMeasureQuantity
    {
        public int Id { get; set; }
        public IngredientsAndNutrients IngredientsAndNutrients { get; set; }
        public Quantity Quantity { get; set; }
        public Measure Measure { get; set; }

        public IngredientMeasureQuantity()
        {
            if (this.IngredientsAndNutrients == null)
                IngredientsAndNutrients = new IngredientsAndNutrients();
            if (this.Quantity == null)
                Quantity = new Quantity();
            if (this.Measure == null)
                Measure = new Measure();
        }
    }
}
