namespace DelikatessenDrehbuch.ShoppingList.Model
{
    public class ShoppingListModel
    {
        public int Id { get; set; }
        public bool IsBought { get; set; } = false;
        public string Name { get; set; }
        public double? Quantity { get; set; } 
        public string UnitOfMeasurement { get; set; }
        public string Category { get; set; }

    }
}
