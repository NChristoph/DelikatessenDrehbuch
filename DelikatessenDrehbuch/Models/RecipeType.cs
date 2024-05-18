using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DelikatessenDrehbuch.Models
{
    public class RecipeType
    {
        public int Id { get; set; } 
        public Recipes Recipes { get; set; }
        public bool Vegan { get; set; }
        public bool Vegetarian { get; set; }
        public bool LowCarb { get; set; }
        public bool BBQ { get; set; }
        public bool Pastry { get; set; }
        public bool Bread { get; set; }
        public bool Cake { get; set; }
        public bool Biscuits { get; set; }
        public bool Cocktails { get; set; }
        public bool Pie { get; set; }
        public bool Diet { get; set; }

       


    }
}
