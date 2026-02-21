using DelikatessenDrehbuch.Services.Interfaces;

namespace DelikatessenDrehbuch.Models
{
    public class MealPlanerModel
    {
       
        public int Index { get; set; }
        public Recipes Recipes { get; set; }
        public bool IsBaseData { get; set; }
     

     
    }
}
