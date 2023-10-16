namespace DelikatessenDrehbuch.Models
{
    public class FullRecipes
    {
        public Recipes Recipes { get; set; }
        public virtual List<IngredientHandlerModel> IngredientHandler { get; set; }
        public virtual List<Like>? Likes { get; set; }
        public virtual List<Recession>? Recession { get; set; }
        public virtual List<Measure> Measure { get; set; }
     
        //public virtual List<string> MeasureNames { get; set; }  

        public FullRecipes()
        {
            Recipes = new Recipes();
            IngredientHandler = new List<IngredientHandlerModel>();
            Likes= new List<Like>();    
            Recession = new List<Recession>();
            Measure = new List<Measure>();
            //MeasureNames = new List<string>();  
        }
    }
}
