using Microsoft.ApplicationInsights;

namespace DelikatessenDrehbuch.Models
{
    public class NutrienHandler
    {
        public int Id { get; set; }
        public Ingredient Ingredient { get; set; }
        public Nutrients Nutrients { get; set; }
        public Quantity Quantity { get; set; }
        public Measure Metrics { get; set; }
    }
}
