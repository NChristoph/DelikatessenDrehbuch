namespace DelikatessenDrehbuch.Models
{
    public class ModelForMealPlanSettingView
    {
        public PersonalMealPlanSettings PersonalMealPlanSettings { get; set; } = new();
        public List<Ingredient> Ingredients { get; set; } = new();
        public bool HaveCatchContent { get; set; }
    }
}
