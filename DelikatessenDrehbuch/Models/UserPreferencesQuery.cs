namespace DelikatessenDrehbuch.Models
{
    public class UserPreferencesQuery
    {
        public int Id { get; set; }
        public string UserEmail { get; set; }
        public string Query { get; set; }
        public int Count { get; set; }
    }
}
