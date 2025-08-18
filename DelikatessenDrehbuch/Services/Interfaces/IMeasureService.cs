using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IMeasureService
    {
        public Task<Measure> GetorCreateMeasureAsync(string measure);
        public Task<List<Measure>> GetMeasureFromDbAsync();
    }
}
