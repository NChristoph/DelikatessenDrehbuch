﻿using Azure;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace DelikatessenDrehbuch.Controllers
{
    public class SelectedRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        public SelectedRecipeController(ApplicationDbContext dbContext, HelpfulMethods helpfulMethods)
        {
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
        }

        public IActionResult Index(int id)
        {
           _helpfulMethods.CreateUserPreferencesRecipe(id, User.Identity.Name, _context);
            return View(_helpfulMethods.GetFullRecipeById(_context, id));
        }


        public async Task<IActionResult> AddOrRemoveLike(int id)
        {
            var currentUserName = User.Identity.Name;
            var recipe = await _helpfulMethods.GetRecipeFromDbById(_context, id);
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);

            if (recipe == null)
                return BadRequest();

            if (like == null)
                AddLike(currentUserName, id, recipe);
            else
                RemoveLike(like, recipe);

            return RedirectToAction("Index", new { id });
        }

        private void AddLike(string currentUserName, int id, Recipes recipes)
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

        private void RemoveLike(Like like, Recipes recipe)
        {
            recipe.LikeCount--;
            _context.Likes.Remove(like);
            _context.SaveChanges();
        }

        public async Task<IActionResult> SaveRecessionInDB(int id, string assessment)
        {

            if (id == null)
                return BadRequest();

            Recession newRecession = new();
            newRecession.Id = 0;
            newRecession.CreationDate = DateTime.Now;
            newRecession.UserEmail = User.Identity.Name;
            newRecession.Assessment = assessment;
            newRecession.Recipes = await _helpfulMethods.GetRecipeFromDbById(_context, id);

            _context.Recessions.Add(newRecession);
            _context.SaveChanges();
            return RedirectToAction("Index", new { id = id });
        }
    }
}
