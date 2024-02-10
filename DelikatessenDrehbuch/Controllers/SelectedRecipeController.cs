﻿using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Controllers
{
    public class SelectedRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SelectedRecipeController(ApplicationDbContext dbContext)
        {
            _context = dbContext;
        }
        public IActionResult Index(int id)
        {
            return View(HelpfulMethods.GetFullRecipeById(_context,id));
        }



        public IActionResult AddOrRemoveLike(int id)
        {
            var currentUserName = User.Identity.Name;
            var recipe = _context.Recipes.SingleOrDefault(x => x.Id == id);
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);

            if (recipe == null)
             return BadRequest();



            if (like == null)
                AddLike(currentUserName, id, recipe);
            else
                RemoveLike(like,recipe);
            
            return RedirectToAction("Index",new { id });
        }

        private void AddLike(string currentUserName,int id,Recipes recipes)
        {
            var like = new Like
            {
                Id = 0,
                UserMail = currentUserName,
                Recipe = recipes,
               

            };

            recipes.LikeCount++;

            _context.Likes.Add(like);
            _context.SaveChanges();
        }

        private void RemoveLike(Like like,Recipes recipe)
        {
            recipe.LikeCount--;
            _context.Likes.Remove(like);
            _context.SaveChanges();
        }
    }
}
