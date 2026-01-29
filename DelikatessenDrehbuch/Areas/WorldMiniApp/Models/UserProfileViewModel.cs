using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class UserProfileViewModel
    {
        public WorldAppUser User { get; set; }

        // Immer sichtbar
        public List<WorldUserPosting> LikedRecipes { get; set; }
        public List<WorldAppUser> Following { get; set; } 

        // Nur für "orb" User sichtbar
        public List<WorldUserPosting> MyVideos { get; set; } = new List<WorldUserPosting>();
        public int FollowerCount { get; set; } // Wer folgt mir?
    }
}
