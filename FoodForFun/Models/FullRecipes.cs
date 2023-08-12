namespace FoodForFun.Models
{
    public class FullRecipes
    {
        public Recipes Recipes { get; set; }
        public virtual List<Ingredient> Ingredient { get; set; }
      

        public FullRecipes()
        {
            Recipes = new Recipes();
            Ingredient = new List<Ingredient>();
        }
    }
}
