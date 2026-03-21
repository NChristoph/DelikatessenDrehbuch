using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Polly;
using System.Threading.Tasks;

namespace DelikatessenDrehbuch.Services
{
    public class QueryService : IQueryService
    {
        private readonly ApplicationDbContext _context;

        public QueryService(ApplicationDbContext context)
        {
            _context = context;

        }
        public async Task<List<string>> GetQuerysFromDbByRecipeIdAsync(int id)
        {
            return await _context.QueryHandler.Where(x => x.Recipe.Id == id)
                                        .Select(x => x.Query.Query)
                                        .ToListAsync();
        }

        public async Task DeleteQuerysByRecipesId(int id)
        {
            var queryhandlerFromDb = _context.QueryHandler.Where(x => x.Recipe.Id == id).ToList();
            _context.RemoveRange(queryhandlerFromDb);
            await _context.SaveChangesAsync();
        }

        public async Task CreateQuerysAsync(List<string> querys)
        {
            var querysFromDb = _context.Querys.Select(x => x.Query.ToLower().Trim());
            foreach (var query in querys)
            {
                if (!querysFromDb.Contains(query.ToLower().Trim()))
                {
                    var newQuery = new Queries()
                    {
                        Id = 0,
                        Query = query,
                    };

                    await _context.Querys.AddAsync(newQuery);
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task CreateQuaryHandlerAsync(int recipesId, List<string> querys)
        {
            await DeleteQuerysByRecipesId(recipesId);
            await CreateQuerysAsync(querys);

            foreach (var query in querys)
            {
                var quaryHandler = new QueryHandler()
                {
                    Id = 0,
                    Recipe = await _context.Recipes.SingleOrDefaultAsync(x => x.Id == recipesId),
                    Query = await _context.Querys.SingleOrDefaultAsync(x => x.Query.ToLower() == query.ToLower()),
                };

                await _context.QueryHandler.AddAsync(quaryHandler);
            }

            await _context.SaveChangesAsync();
        }

        public Task<List<int>> GetRecipeIdsByQuerry(string query)
        {
            return _context.QueryHandler
                           .Where(x => x.Query.Query.ToLower().Trim() == query.ToLower().Trim())
                           .Select(x => x.Recipe.Id)
                           .ToListAsync();
        }
    }
}
