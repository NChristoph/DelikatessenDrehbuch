using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using System.Security.Policy;

namespace DelikatessenDrehbuch.Services
{
    public class AdminControllerModelService:IAdminControllerModelService
    {
        public readonly ApplicationDbContext _context;
        public readonly IRecipesService _recipesService;
        public AdminControllerModelService(ApplicationDbContext context,IRecipesService recipesService)
        {
            _context = context;
            _recipesService = recipesService;   
        }

        public AdminControllerModel GetAdminControlerModel()
        {
            AdminControllerModel model = new()
            {
                SupportMessage = _context.SupportMessage.ToList(),
                RecipesCount = _recipesService.GetRecipesCount(),
                UserCount = _context.Users.Count(),
                PremiumUser = _context.Users.Where(user => _context.UserRoles
                                                  .Any(ur => ur.UserId == user.Id && _context.Roles
                                                  .Any(r => r.Id == ur.RoleId && r.Name == "PremiumUser")))
                                             .Count()
            };

            return model;
        }
    }


}
