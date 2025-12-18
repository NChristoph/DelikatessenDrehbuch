using DelikatessenDrehbuch.Data;
using Microsoft.Build.Framework;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldAppUser
    {
        public int Id { get; set; }
        public string UserHash { get; set; }
        public string IsVerified { get; set; }
        public DateTime Lastlogin { get; set; }= DateTime.MinValue;

        //[NotMapped]
        //private readonly ApplicationDbContext _context;
        //public WorldAppUser(ApplicationDbContext context)
        //{
        //    _context = context;
        //}
    }
}
