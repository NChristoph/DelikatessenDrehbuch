using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IQuantityService
    {
        Task<Quantity> GetOrCreateQuantityAsync(double quantity);
    }
}
