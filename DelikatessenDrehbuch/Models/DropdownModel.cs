namespace DelikatessenDrehbuch.Models
{
    public class DropdownModel
    {
        public IngredientHandlerModel IngredientHandler { get; set; } = new IngredientHandlerModel();
        public List<Measure> Measure { get; set;}
        public int Index {  get; set; } 
        public DropdownModel()
        {
            Measure = new List<Measure>();
        }
    }
}
