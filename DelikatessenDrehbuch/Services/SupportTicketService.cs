
using DelikatessenDrehbuch.Data;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class SupportTicketService:ISupportTicketService
    {
        private readonly ApplicationDbContext _context;
        public SupportTicketService( ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task DeleteSupportTicketByIdAsync(int id)
        {
            if (id == 0)
                throw new Exception("Supportticket id kann nicht 0 sein");
            var supportMessageFromDb = _context.SupportMessage.SingleOrDefault(x => x.Id == id);

            if (supportMessageFromDb == null)
                 throw new Exception("Supportticket nicht gefunden.");

            _context.Remove(supportMessageFromDb);
            await _context.SaveChangesAsync();
        }
    }
}
