using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class RecessionsService : IRecessionsService
    {
        private readonly ApplicationDbContext _context;
       
        public RecessionsService( ApplicationDbContext context)
        {
            _context = context;
            
        }
        public async Task<List<Recession>> GetRecessionsByRecipeIdFromDbAsync(int id)
        {
            return await _context.Recessions.Where(x => x.Recipes.Id == id).Take(50).ToListAsync();
           
        }

        public async Task SaveNewRecessionInDbAsync(int recipesId,string assessment,string userName)
        {
            Recession newRecession = new()
            {
                Id = 0,
                CreationDate = DateTime.Now,
                UserEmail = userName,
                Assessment = assessment,
                Recipes = await _context.Recipes.SingleAsync(x=>x.Id==recipesId),
            };

            await _context.Recessions.AddAsync(newRecession);
            await _context.SaveChangesAsync();
        }
    }
}
