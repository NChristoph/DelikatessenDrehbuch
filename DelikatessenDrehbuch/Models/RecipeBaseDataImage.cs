namespace DelikatessenDrehbuch.Models
{
    public class RecipeBaseDataImage
    {
        public int Id { get; set; }
        public RecipeBaseData Recipe { get; set; }
        public string Image { get; set; }
        public bool WorldAppImage { get; set; }
    }
}
