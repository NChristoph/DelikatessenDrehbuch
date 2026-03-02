namespace DelikatessenDrehbuch.Models
{
    public class AdminControllerModel
    {


        public List<SupportMessage> SupportMessage { get; set; }

        public int UserCount { get; set; }
        public int PremiumUser { get; set; }
        public int RecipesCount { get; set; }

        public AdminControllerModel()
        {
            SupportMessage = new List<SupportMessage>();
          
        }
    }
}
