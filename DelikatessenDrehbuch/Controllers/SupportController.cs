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
        [ValidateAntiForgeryToken]
        public IActionResult CreatSupportMessage([FromBody] SupportMessage message)
        {
            if (message == null)
                return BadRequest("No Message input");

            _context.SupportMessage.Add(message);
            _context.SaveChanges();

            return Ok();
        }

      
    }
}
