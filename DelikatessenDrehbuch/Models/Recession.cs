namespace DelikatessenDrehbuch.Models
{
    public class Recession
    {
        public int Id { get; set; }
        public string UserEmail { get; set; }
        public Recipes Recipe { get; set; }
        public string Assessment { get; set; }
    }
}
