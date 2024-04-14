namespace DelikatessenDrehbuch.Models
{
    public class SupportMessage
    {
        
        public int Id { get; set; }
        public string Type { get; set; }
        public int TypeId { get; set; }
        public string SenderEmail { get; set; }
        public string Message { get; set; }
    }
}
