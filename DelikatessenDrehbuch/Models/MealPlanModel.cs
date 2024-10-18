namespace DelikatessenDrehbuch.Models
{
    public class MealPlanModel
    {
        public MealPlan MealPlan { get; set; }
        public List<Recipes> Recipes { get; set; } = new List<Recipes>();
    }
}
