using DelikatessenDrehbuch.Services.Interfaces;

namespace DelikatessenDrehbuch.Models
{
    public class PersonalMealPlanRecipeModel
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public Recipes Recipes { get; set; }
        public virtual List<IngredientHandlerModel> Ingredients { get; set; } = new();

       

    }
}
