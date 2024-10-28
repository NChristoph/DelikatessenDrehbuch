using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DelikatessenDrehbuch.Models
{
    public class NutrientsModel
    {
       public  IngredientNutrientHandler IngredientNutrientHandler { get; set; }
       public List<NutrientsHandler> Nutrients { get; set;} =new List<NutrientsHandler>();

        public NutrientsModel()
        {
            IngredientNutrientHandler = new();

        }
    }
}
