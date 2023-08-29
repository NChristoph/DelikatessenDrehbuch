namespace DelikatessenDrehbuch.Models
{
    public class IngredientHandlerModel
    {
        public int Id { get; set; }
        public Ingredient Ingredient { get; set; }
        public Quantity Quantity { get; set; }
        public Measure Measure { get; set; }
    }
}
