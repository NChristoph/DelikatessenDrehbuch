using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services
{
    
    public class QuantityService : IQuantityService
    {
        private readonly ApplicationDbContext _context;
        public QuantityService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Quantity> GetOrCreateQuantityAsync(double quantity)
        {
            var quantityFromDb = await _context.Quantities.SingleOrDefaultAsync(x => x.Quantitys == quantity);

            if (quantityFromDb != null)
                return quantityFromDb;
            else
            {
                quantityFromDb = new()
                {
                    Id = 0,
                    Quantitys = quantity
                };

               await _context.Quantities.AddAsync(quantityFromDb);
               await _context.SaveChangesAsync();
                return quantityFromDb;
            }
        }
    }
}
