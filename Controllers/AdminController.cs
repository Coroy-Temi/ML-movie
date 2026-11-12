using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ML_MOVIE.Data;
using ML_MOVIE.Entities;
using ML_MOVIE.Infrastructure;
using ML_MOVIE.Models.Admin;

namespace ML_MOVIE.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly AppDbContext _dbContext;

    public AdminController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        return View(await BuildDashboardViewModelAsync());
    }

    [HttpPost("/admin/homepage-updates")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHomepageUpdate([Bind(Prefix = "NewUpdate")] HomepageUpdateEditViewModel newUpdate)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildDashboardViewModelAsync(newUpdate));
        }

        _dbContext.HomepageUpdates.Add(new HomepageUpdate
        {
            Title = newUpdate.Title.Trim(),
            Body = newUpdate.Body.Trim(),
            Category = newUpdate.Category.Trim(),
            IsPublished = newUpdate.IsPublished
        });

        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Homepage update published.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/homepage-updates/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomepageUpdate(int id)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var update = await _dbContext.HomepageUpdates.FirstOrDefaultAsync(item => item.Id == id);
        if (update is null)
        {
            return NotFound();
        }

        update.IsPublished = !update.IsPublished;
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = update.IsPublished ? "Homepage update published again." : "Homepage update hidden from the homepage.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/homepage-updates/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHomepageUpdate(int id)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var update = await _dbContext.HomepageUpdates.FirstOrDefaultAsync(item => item.Id == id);
        if (update is null)
        {
            return NotFound();
        }

        _dbContext.HomepageUpdates.Remove(update);
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Homepage update deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/grant-admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantAdmin([Bind(Prefix = "GrantAdmin")] GrantAdminViewModel grantAdmin)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildDashboardViewModelAsync(grantAdmin: grantAdmin));
        }

        var normalizedEmail = grantAdmin.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Email == normalizedEmail);
        if (user is null)
        {
            ModelState.AddModelError("GrantAdmin.Email", "No user was found with that email.");
            return View("Index", await BuildDashboardViewModelAsync(grantAdmin: grantAdmin));
        }

        if (user.IsAdmin)
        {
            TempData["StatusMessage"] = $"{user.Email} is already an admin.";
            return RedirectToAction(nameof(Index));
        }

        user.IsAdmin = true;
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = $"{user.Email} can now access the admin area.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminDashboardViewModel> BuildDashboardViewModelAsync(
        HomepageUpdateEditViewModel? pendingUpdate = null,
        GrantAdminViewModel? grantAdmin = null)
    {
        var updates = await _dbContext.HomepageUpdates
            .OrderByDescending(item => item.CreatedUtc)
            .Select(item => new HomepageUpdateItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Body = item.Body,
                Category = item.Category,
                IsPublished = item.IsPublished,
                CreatedUtc = item.CreatedUtc
            })
            .ToArrayAsync();

        return new AdminDashboardViewModel
        {
            Updates = updates,
            NewUpdate = pendingUpdate ?? new HomepageUpdateEditViewModel(),
            GrantAdmin = grantAdmin ?? new GrantAdminViewModel(),
            UserCount = await _dbContext.Users.CountAsync(),
            VerifiedUserCount = await _dbContext.Users.CountAsync(item => item.IsEmailVerified),
            SessionCount = await _dbContext.RecommendationSessions.CountAsync(),
            ContactMessageCount = await _dbContext.ContactMessages.CountAsync()
        };
    }
}
