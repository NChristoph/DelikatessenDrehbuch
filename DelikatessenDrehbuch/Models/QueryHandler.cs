using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class QueryHandler
    {
        public int Id { get; set; }
        public Recipes Recipe { get; set; }
        public Querys Query { get; set; }
       


    }
}
