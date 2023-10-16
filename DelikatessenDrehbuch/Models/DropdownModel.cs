namespace DelikatessenDrehbuch.Models
{
    public class DropdownModel
    {
        public IngredientHandlerModel IngredientHandler { get; set; } = new IngredientHandlerModel();
        public List<string> MeasureNames { get; set;}
    }
}
