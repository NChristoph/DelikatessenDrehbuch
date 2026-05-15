using DelikatessenDrehbuch.Data;
using Microsoft.Build.Framework;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldAppUser
    {
        public int Id { get; set; }
        public string? UserHash { get; set; }
        public string? UserName { get; set; }
        public string? IsVerified { get; set; }
        public DateTime? Lastlogin { get; set; }
        public bool RememberLogin { get; set; }
        public decimal WildCoinBalance { get; set; }
        public string? Bio { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
