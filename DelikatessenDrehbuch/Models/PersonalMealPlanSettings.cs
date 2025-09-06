namespace DelikatessenDrehbuch.Models
{
    public class PersonalMealPlanSettings
    {
        public int DayCount { get; set; }
        public int PersonCount { get; set; }
        public bool Vegan { get; set; }
        public bool NoVegan { get; set; }
        public bool Vegetarisch { get; set; }
        public bool NoVegetarisch { get; set; }
        public bool NoPork{ get; set; }
        public bool NoFish{ get; set; }
        public bool CookingTimeOne { get; set; }
        public bool CookingTimeTwo { get; set; }
        public List<int> IngredientIds { get; set; }
        public List<IngredientHandlerModel> Ingredients { get; set; }

    }
}
