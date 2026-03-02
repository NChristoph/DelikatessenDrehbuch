using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class MeasureService : IMeasureService
    {
        private readonly ApplicationDbContext _context;
        public MeasureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<Measure>> GetMeasureFromDbAsync()
        {
            return  _context.Metrics.ToListAsync();
        }

        public async Task <Measure> GetorCreateMeasureAsync(string measure)
        {

            var measureFromDb = await _context.Metrics.SingleAsync(x => x.UnitOfMeasurement.ToLower().Trim() == measure.ToLower().Trim()
                                                                || x.UnitOfMeasurement.ToLower().Trim() == measure.Trim().ToLower() + ".");

            if (measureFromDb != null)
                return measureFromDb;
            else
            {
                measureFromDb = new Measure()
                {
                    Id = 0,
                    UnitOfMeasurement = measure
                };
               await _context.Metrics.AddAsync(measureFromDb);
               await  _context.SaveChangesAsync();

                return measureFromDb;
            }
        }
    }
}
