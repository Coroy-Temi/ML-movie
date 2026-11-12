using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ML_MOVIE.Data;
using ML_MOVIE.Entities;
using ML_MOVIE.Infrastructure;
using ML_MOVIE.Models;
using ML_MOVIE.Models.Settings;
using ML_MOVIE.Services;

namespace ML_MOVIE.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IEmailVerificationService _emailVerificationService;

    public UserController(
        AppDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher,
        IEmailVerificationService emailVerificationService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _emailVerificationService = emailVerificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var outboxPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "outbox", $"verification-{user.Id}.txt");
        var outboxPreview = System.IO.File.Exists(outboxPath)
            ? await System.IO.File.ReadAllTextAsync(outboxPath)
            : null;

        return View(BuildSettingsViewModel(user, outboxPreview));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileSettingsViewModel profile)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", BuildSettingsViewModel(user, null, profile: profile));
        }

        user.DisplayName = profile.DisplayName.Trim();
        user.ThemePreference = profile.Theme == ThemeOption.Light ? "light" : "dark";
        await _dbContext.SaveChangesAsync();
        await RefreshSignInAsync(user);
        TempData["StatusMessage"] = "Profile and theme updated.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] PasswordSettingsViewModel password)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", BuildSettingsViewModel(user, null, password: password));
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Password.CurrentPassword", "Current password is incorrect.");
            return View("Settings", BuildSettingsViewModel(user, null, password: password));
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, password.NewPassword);
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Password updated.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendVerificationCode()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var targetEmail = string.IsNullOrWhiteSpace(user.PendingEmail) ? user.Email : user.PendingEmail;
        await _emailVerificationService.SendVerificationCodeAsync(user, targetEmail);
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Verification code sent.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail([Bind(Prefix = "Verification")] EmailVerificationViewModel verification)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(verification.Code) || !_emailVerificationService.VerifyCode(user, verification.Code))
        {
            ModelState.AddModelError("Verification.Code", "The verification code is invalid or expired.");
            return View("Settings", BuildSettingsViewModel(user, null, verification: verification));
        }

        if (!string.IsNullOrWhiteSpace(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
            user.PendingEmail = string.Empty;
        }

        user.IsEmailVerified = true;
        await _dbContext.SaveChangesAsync();
        await RefreshSignInAsync(user);
        TempData["StatusMessage"] = "Email verified successfully.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail([Bind(Prefix = "EmailChange")] EmailChangeViewModel emailChange)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", BuildSettingsViewModel(user, null, emailChange: emailChange));
        }

        var normalizedEmail = emailChange.NewEmail.Trim().ToLowerInvariant();
        if (normalizedEmail == user.Email)
        {
            ModelState.AddModelError("EmailChange.NewEmail", "That is already your current email.");
            return View("Settings", BuildSettingsViewModel(user, null, emailChange: emailChange));
        }

        if (await _dbContext.Users.AnyAsync(item => item.Email == normalizedEmail))
        {
            ModelState.AddModelError("EmailChange.NewEmail", "That email is already in use.");
            return View("Settings", BuildSettingsViewModel(user, null, emailChange: emailChange));
        }

        user.PendingEmail = normalizedEmail;
        user.IsEmailVerified = false;
        await _emailVerificationService.SendVerificationCodeAsync(user, normalizedEmail);
        await _dbContext.SaveChangesAsync();
        await RefreshSignInAsync(user);
        TempData["StatusMessage"] = "A verification code has been sent to your new email.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactUs([Bind(Prefix = "ContactUs")] ContactUsViewModel contactUs)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", BuildSettingsViewModel(user, null, contactUs: contactUs));
        }

        _dbContext.ContactMessages.Add(new ContactMessage
        {
            UserId = user.Id,
            Subject = contactUs.Subject.Trim(),
            Message = contactUs.Message.Trim()
        });
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Thanks for the feedback. Your message has been saved.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount([Bind(Prefix = "DeleteAccount")] DeleteAccountViewModel deleteAccount)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, deleteAccount.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("DeleteAccount.Password", "Password is incorrect.");
            return View("Settings", BuildSettingsViewModel(user, null, deleteAccount: deleteAccount));
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Library()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var sessions = await _dbContext.RecommendationSessions
            .Where(item => item.UserId == user.Id)
            .OrderByDescending(item => item.CreatedUtc)
            .Select(item => new RecommendationSessionSummary
            {
                DetectedEmotion = item.DetectedEmotion,
                EmotionSource = item.EmotionSource,
                GenreFilters = string.IsNullOrWhiteSpace(item.GenreFilters) ? "No extra filters" : item.GenreFilters,
                RecommendedTitles = item.RecommendedTitles,
                CreatedUtc = item.CreatedUtc
            })
            .ToArrayAsync();

        return View(new LibraryViewModel
        {
            DisplayName = user.DisplayName,
            Sessions = sessions
        });
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.GetUserId();
        return userId is null ? null : await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId.Value);
    }

    private SettingsViewModel BuildSettingsViewModel(
        AppUser user,
        string? outboxPreview,
        ProfileSettingsViewModel? profile = null,
        PasswordSettingsViewModel? password = null,
        EmailVerificationViewModel? verification = null,
        EmailChangeViewModel? emailChange = null,
        DeleteAccountViewModel? deleteAccount = null,
        ContactUsViewModel? contactUs = null)
    {
        return new SettingsViewModel
        {
            Profile = profile ?? new ProfileSettingsViewModel
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsEmailVerified = user.IsEmailVerified,
                CreatedUtc = user.CreatedUtc,
                Theme = string.Equals(user.ThemePreference, "light", StringComparison.OrdinalIgnoreCase) ? ThemeOption.Light : ThemeOption.Dark
            },
            Password = password ?? new PasswordSettingsViewModel(),
            Verification = verification ?? new EmailVerificationViewModel { PendingEmail = user.PendingEmail },
            EmailChange = emailChange ?? new EmailChangeViewModel(),
            DeleteAccount = deleteAccount ?? new DeleteAccountViewModel(),
            ContactUs = contactUs ?? new ContactUsViewModel(),
            VerificationDestination = string.IsNullOrWhiteSpace(user.PendingEmail) ? user.Email : user.PendingEmail,
            OutboxPreview = outboxPreview
        };
    }

    private async Task RefreshSignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new("theme", user.ThemePreference),
            new("email_verified", user.IsEmailVerified.ToString().ToLowerInvariant()),
            new("is_admin", user.IsAdmin.ToString().ToLowerInvariant())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }
}
