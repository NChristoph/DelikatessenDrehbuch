namespace DelikatessenDrehbuch.Models
{
    public class IngredientHandlerModel
    {
        public int Id { get; set; }
        public Ingredient Ingredient { get; set; }
        public Quantity Quantity { get; set; }
        public Measure Measure { get; set; }

        public IngredientHandlerModel()
        {
            if(this.Ingredient == null)
                Ingredient=new Ingredient();
            if(this.Quantity == null)
                Quantity=new Quantity();
            if(this.Measure == null)
                Measure=new Measure();
        }
    }
}
