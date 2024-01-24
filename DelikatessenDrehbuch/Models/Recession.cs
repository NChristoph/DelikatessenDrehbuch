using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class Recession
    {
        public int Id { get; set; }
        public string UserEmail { get; set; }
       
        public Recipes Recipes { get; set; }
        
        public string Assessment { get; set; }
    }
}
