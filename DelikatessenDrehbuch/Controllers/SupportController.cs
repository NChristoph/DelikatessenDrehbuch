using Microsoft.AspNetCore.Mvc;
using DelikatessenDrehbuch.StaticScripts;
using System.Runtime.CompilerServices;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Data;

namespace DelikatessenDrehbuch.Controllers
{

    public class SupportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SupportController(ApplicationDbContext context)
        {

            _context = context;
        }


        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreatSupportMessage([FromBody] SupportMessage message)
        {
            if (message == null)
                return BadRequest();

            _context.SupportMessage.Add(message);
            _context.SaveChanges();

            return Ok();
        }

        public IActionResult DeleteSupportTicket(int id)
        {
            if (id == 0)
                return BadRequest();
            var supportMessageFromDb = _context.SupportMessage.SingleOrDefault(x => x.Id == id);

            if (supportMessageFromDb == null)
                return BadRequest();

            _context.Remove(supportMessageFromDb);
            _context.SaveChanges();

            var newSupportMessageFromDb = _context.SupportMessage.ToList();
            return PartialView("~/Views/MyRecipes/_AdminSupportMessage.cshtml", newSupportMessageFromDb);
        }
    }
}
