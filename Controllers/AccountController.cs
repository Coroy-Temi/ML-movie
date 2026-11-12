using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ML_MOVIE.Data;
using ML_MOVIE.Entities;
using ML_MOVIE.Models.Auth;
using ML_MOVIE.Services;

namespace ML_MOVIE.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IEmailVerificationService _emailVerificationService;

    public AccountController(
        AppDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher,
        IEmailVerificationService emailVerificationService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _emailVerificationService = emailVerificationService;
    }

    [HttpGet("/account")]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Home");
        }

        return View(new AuthViewModel
        {
            Login = new LoginViewModel(),
            Register = new RegisterViewModel()
        });
    }

    [HttpPost("/account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([Bind(Prefix = "Login")] LoginViewModel login)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", BuildAuthModel(login, new RegisterViewModel()));
        }

        var normalizedEmail = login.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Email == normalizedEmail);
        if (user is null)
        {
            ModelState.AddModelError("Login.Email", "No account was found for that email.");
            return View("Index", BuildAuthModel(login, new RegisterViewModel()));
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Login.Password", "That password is not correct.");
            return View("Index", BuildAuthModel(login, new RegisterViewModel()));
        }

        await SignInAsync(user, login.RememberMe);
        return RedirectToAction("Dashboard", "Home");
    }

    [HttpPost("/account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([Bind(Prefix = "Register")] RegisterViewModel register)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", BuildAuthModel(new LoginViewModel(), register));
        }

        var normalizedEmail = register.Email.Trim().ToLowerInvariant();
        var existingUser = await _dbContext.Users.AnyAsync(item => item.Email == normalizedEmail);
        if (existingUser)
        {
            ModelState.AddModelError("Register.Email", "That email already has an account.");
            return View("Index", BuildAuthModel(new LoginViewModel(), register));
        }

        var isFirstUser = !await _dbContext.Users.AnyAsync();

        var user = new AppUser
        {
            DisplayName = register.DisplayName.Trim(),
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            IsEmailVerified = false,
            IsAdmin = isFirstUser
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, register.Password);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _emailVerificationService.SendVerificationCodeAsync(user, user.Email);
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Account created. A verification code has been prepared for your email.";

        await SignInAsync(user, true);
        return RedirectToAction("Settings", "User");
    }

    [HttpPost("/account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private AuthViewModel BuildAuthModel(LoginViewModel login, RegisterViewModel register)
    {
        return new AuthViewModel
        {
            Login = login,
            Register = register
        };
    }

    public async Task SignInAsync(AppUser user, bool rememberMe)
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
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }
}
