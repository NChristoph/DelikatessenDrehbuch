namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserAbo
    {
        public int Id { get; set; }
        public WorldAppUser WorldUser { get; set; }
        public WorldAppUser Creator { get; set; }
    }
}
