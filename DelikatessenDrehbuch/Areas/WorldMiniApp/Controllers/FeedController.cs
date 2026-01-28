using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class FeedController : Controller
    {

        private readonly ApplicationDbContext _context;

        public FeedController(ApplicationDbContext context)
        {
         _context = context;   
        }
        //TODO:Likecount zu basedata recipe hinzufügen und abo system auch machen neue column auserdem brauchen 
        //wir noch eine ide damit die likes rot sind wen wir sie geliket haben
        public async Task<IActionResult> Index(string filter = "feed", string userHash = "", int scrollToId = 0)
        {
            List<WorldUserPosting> model = new List<WorldUserPosting>();

            // Basis-Query: Wir laden Posting + Rezept-Infos (aber KEINE Bilder-Joins mehr nötig!)
            var baseQuery = _context.WorldUserPosting
                .AsNoTracking()
                .Include(p => p.Recipe);

            switch (filter)
            {
                case "likes":
                    if (!string.IsNullOrEmpty(userHash))
                    {
                        var currentUser = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
                        if (currentUser != null)
                        {
                            var likes = _context.WorldUserLike.Where(x => x.WorldAppUser.UserHash == currentUser.UserHash).Select(x=>x.Recipe.Id);
                            
                            model = await _context.WorldUserPosting.Where(x=>likes.Contains(x.Recipe.Id)).Include(x=>x.Recipe).ToListAsync();
                        
                        }
                    }
                    break;

                case "myvideos":
                    if (!string.IsNullOrEmpty(userHash))
                    {
                        model = await baseQuery
                            .Where(p => p.CreatorId == userHash)
                            .OrderByDescending(p => p.CreationTime)
                            .ToListAsync();
                    }
                    break;

                case "feed":
                default:
                    model = await baseQuery
                        .OrderByDescending(p => p.Id)
                        .Take(20)
                        .ToListAsync();
                    break;
            }
            foreach (var item in model)
            {
               item.Source=ChangePath(item.Source);
            }
            ViewData["ScrollToId"] = scrollToId;

            return View(model);
        }


        private string ChangePath(string path)
        {

            if (string.IsNullOrEmpty(path)) return path;

            // Ihre Konstanten (am besten oben in der Klasse definieren, aber hier geht es auch)
            string oldDomain = "blobdelikatessendrehbuch.blob.core.windows.net";
            string newCdnDomain = "DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net";


            if (path.Contains(oldDomain))
            {
                return path.Replace(oldDomain, newCdnDomain);
            }

            return path;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike([FromForm] string userHash, int recipeId)
        {
            await AddOrRemoveLike(userHash, recipeId);

            return Ok();
        }

        private async Task AddOrRemoveLike(string userHash, int recipeId)
        {
            try
            {
                var like = await _context.WorldUserLike
                               .FirstOrDefaultAsync(x => x.WorldAppUser.UserHash == userHash && x.Recipe.Id == recipeId);
                var recipe = await _context.RecipeBaseData.FirstOrDefaultAsync(x => x.Id == recipeId);

                if (like != null)
                {
                    _context.WorldUserLike.Remove(like);
                    recipe.LikeCount--;
                }
                else
                {
                    var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == userHash);


                    if (user != null && recipe != null)
                    {
                        WorldUserLike newLike = new WorldUserLike()
                        {
                            Recipe = recipe,
                            WorldAppUser = user
                        };
                        recipe.LikeCount++;
                        await _context.WorldUserLike.AddAsync(newLike);
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow([FromForm] string userHash, [FromForm] string creatorId)
        {

            var currentUser = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            var creator = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == creatorId);

            if (currentUser == null || creator == null) return BadRequest("User oder Creator nicht gefunden");
            if (currentUser.Id == creator.Id) return BadRequest("Du kannst dir nicht selbst folgen");


            var existingAbo = await _context.WorldUserAbo
                .FirstOrDefaultAsync(x => x.WorldUser.Id == currentUser.Id && x.Creator.Id == creator.Id);

            if (existingAbo != null)
            {
                _context.WorldUserAbo.Remove(existingAbo);
            }
            else
            {
                var newAbo = new WorldUserAbo
                {
                    WorldUser = currentUser,
                    Creator = creator
                };
                await _context.WorldUserAbo.AddAsync(newAbo);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }


        public async Task<IActionResult> MyProfile(string userHash)
        {
            if (string.IsNullOrEmpty(userHash)) return RedirectToAction("Index");

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (user == null) return NotFound();

            // --- BASIS DATEN (Für alle) ---

            // 1. Likes laden
            var likedRecipes = await _context.WorldUserLike
                .Where(l => l.WorldAppUser.Id == user.Id)
                .Include(l => l.Recipe).ThenInclude(r => r.Images)
                .Select(l => l.Recipe)
                .ToListAsync();

            // 2. Abos laden (Wen verfolge ich?)
            var following = await _context.WorldUserAbo
                .Where(a => a.WorldUser.Id == user.Id)
                .Include(a => a.Creator)
                .Select(a => a.Creator)
                .ToListAsync();

            // --- ORB / CREATOR DATEN (Nur wenn verifiziert) ---

            var myVideos = new List<WorldUserPosting>();
            int followerCount = 0;

            // Prüfung auf "orb"
            if (user.IsVerified == "orb" || user.UserHash == "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839")
            {
                // 3. Eigene Videos laden
                myVideos = await _context.WorldUserPosting
                    .Where(p => p.CreatorId == user.UserHash) // Oder User.Id, je nach deiner DB
                    .Include(p => p.Recipe).ThenInclude(r => r.Images)
                    .OrderByDescending(p => p.CreationTime)
                    .ToListAsync();

                // 4. Follower zählen (Wer folgt mir?)
                followerCount = await _context.WorldUserAbo
                    .CountAsync(a => a.Creator.Id == user.Id);
            }

            var model = new UserProfileViewModel
            {
                User = user,
                LikedRecipes = likedRecipes,
                Following = following,
                MyVideos = myVideos,
                FollowerCount = followerCount
            };

            return View(model);
        }


    }
}
