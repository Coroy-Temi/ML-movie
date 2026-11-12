using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ML_MOVIE.Data;
using ML_MOVIE.Entities;
using ML_MOVIE.Infrastructure;
using ML_MOVIE.Models;
using ML_MOVIE.Services;

namespace ML_MOVIE.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IMovieRecommendationService _recommendationService;

    public HomeController(AppDbContext dbContext, IMovieRecommendationService recommendationService)
    {
        _dbContext = dbContext;
        _recommendationService = recommendationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var homepageUpdates = await GetPublishedHomepageUpdatesAsync();

        var model = new HomeViewModel
        {
            Emotions = _recommendationService.GetEmotionOptions(),
            Recommendations = _recommendationService.GetPreviewRecommendations(),
            Datasets = _recommendationService.GetDatasetSummaries(),
            AvailableGenres = _recommendationService.GetAvailableGenres(),
            SelectedGenres = Array.Empty<string>(),
            HomepageUpdates = homepageUpdates,
            SelectedEmotion = EmotionType.Happy,
            MovieCount = 6,
            CatalogSize = _recommendationService.GetCatalogSize()
        };

        ViewData["TotalUsers"] = await _dbContext.Users.CountAsync();
        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Dashboard(string? emotion = null, string[]? genres = null, int count = 6)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return RedirectToAction("Index");
        }

        var selectedGenres = genres?
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray() ?? Array.Empty<string>();

        var discovery = _recommendationService.BuildHomeViewModel(emotion, selectedGenres, count, await GetPublishedHomepageUpdatesAsync());
        var user = await _dbContext.Users.FirstAsync(item => item.Id == userId.Value);
        return View(await BuildDashboardViewModelAsync(user, discovery));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureEmotion(FacialEmotionInput input)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return RedirectToAction("Index");
        }

        var user = await _dbContext.Users.FirstAsync(item => item.Id == userId.Value);
        if (!user.IsEmailVerified)
        {
            TempData["StatusMessage"] = "Verify your email in Settings before using facial emotion recommendations.";
            return RedirectToAction("Settings", "User");
        }

        var selectedGenres = string.IsNullOrWhiteSpace(input.GenreFilters)
            ? Array.Empty<string>()
            : input.GenreFilters.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var discovery = _recommendationService.BuildHomeViewModel(input.DetectedEmotion, selectedGenres, 6);
        if (discovery.Recommendations.Count == 0)
        {
            if (IsAjaxRequest())
            {
                return Json(await BuildDashboardResponseAsync(user, discovery, false));
            }

            return View("Dashboard", await BuildDashboardViewModelAsync(user, discovery));
        }

        var session = new RecommendationSession
        {
            UserId = userId.Value,
            DetectedEmotion = discovery.SelectedEmotion.ToString(),
            EmotionSource = string.IsNullOrWhiteSpace(input.EmotionSource) ? "Camera prototype" : input.EmotionSource,
            GenreFilters = string.Join(", ", discovery.SelectedGenres),
            RecommendedTitles = string.Join(" | ", discovery.Recommendations.Select(item => item.Movie.Title)),
            CaptureDataUrl = input.CaptureDataUrl
        };

        _dbContext.RecommendationSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        if (IsAjaxRequest())
        {
            return Json(await BuildDashboardResponseAsync(user, discovery, true));
        }

        return RedirectToAction(nameof(Dashboard), new
        {
            emotion = discovery.SelectedEmotion.ToString(),
            genres = string.Join(",", discovery.SelectedGenres)
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualRecommendation(string emotion, string[]? genres, int count = 6)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return RedirectToAction("Index");
        }

        var user = await _dbContext.Users.FirstAsync(item => item.Id == userId.Value);
        var selectedGenres = genres?
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        var discovery = _recommendationService.BuildHomeViewModel(emotion, selectedGenres, count, await GetPublishedHomepageUpdatesAsync());
        if (discovery.Recommendations.Count == 0)
        {
            if (IsAjaxRequest())
            {
                return Json(await BuildDashboardResponseAsync(user, discovery, false));
            }

            return View("Dashboard", await BuildDashboardViewModelAsync(user, discovery));
        }

        _dbContext.RecommendationSessions.Add(new RecommendationSession
        {
            UserId = user.Id,
            DetectedEmotion = discovery.SelectedEmotion.ToString(),
            EmotionSource = "Manual mood selection",
            GenreFilters = string.Join(", ", discovery.SelectedGenres),
            RecommendedTitles = string.Join(" | ", discovery.Recommendations.Select(item => item.Movie.Title))
        });

        await _dbContext.SaveChangesAsync();

        if (IsAjaxRequest())
        {
            return Json(await BuildDashboardResponseAsync(user, discovery, true));
        }

        return RedirectToAction(nameof(Dashboard), new
        {
            emotion = discovery.SelectedEmotion.ToString(),
            genres = string.Join(",", discovery.SelectedGenres),
            count
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<IReadOnlyList<HomepageUpdateViewModel>> GetPublishedHomepageUpdatesAsync()
    {
        return await _dbContext.HomepageUpdates
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.CreatedUtc)
            .Take(3)
            .Select(item => new HomepageUpdateViewModel
            {
                Title = item.Title,
                Body = item.Body,
                Category = item.Category
            })
            .ToArrayAsync();
    }

    private async Task<DashboardViewModel> BuildDashboardViewModelAsync(AppUser user, HomeViewModel discovery)
    {
        var recentSessions = await GetRecentSessionsAsync(user.Id);

        return new DashboardViewModel
        {
            Discovery = discovery,
            DisplayName = user.DisplayName,
            CameraInput = new FacialEmotionInput
            {
                DetectedEmotion = discovery.SelectedEmotion.ToString(),
                GenreFilters = string.Join(",", discovery.SelectedGenres),
                EmotionSource = "Camera capture"
            },
            IsEmailVerified = user.IsEmailVerified,
            RecentSessions = recentSessions
        };
    }

    private async Task<IReadOnlyList<RecommendationSessionSummary>> GetRecentSessionsAsync(int userId)
    {
        return await _dbContext.RecommendationSessions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedUtc)
            .Take(5)
            .Select(item => new RecommendationSessionSummary
            {
                DetectedEmotion = item.DetectedEmotion,
                EmotionSource = item.EmotionSource,
                GenreFilters = string.IsNullOrWhiteSpace(item.GenreFilters) ? "No extra filters" : item.GenreFilters,
                RecommendedTitles = item.RecommendedTitles,
                CreatedUtc = item.CreatedUtc
            })
            .ToArrayAsync();
    }

    private async Task<object> BuildDashboardResponseAsync(AppUser user, HomeViewModel discovery, bool wasSaved)
    {
        var recentSessions = await GetRecentSessionsAsync(user.Id);
        return new
        {
            displayName = user.DisplayName,
            selectedEmotion = discovery.SelectedEmotion.ToString(),
            movieCount = discovery.MovieCount,
            emptyStateMessage = discovery.EmptyStateMessage,
            wasSaved,
            recommendations = discovery.Recommendations.Select(item => new
            {
                title = item.Movie.Title,
                year = item.Movie.ReleaseYear,
                runtimeMinutes = item.Movie.RuntimeMinutes,
                rating = item.Movie.Rating.ToString("0.0"),
                genres = item.Movie.Genres.Replace("|", " / "),
                overview = item.Movie.Overview,
                reason = item.Reason,
                score = item.Score.ToString("0.0"),
                posterUrl = item.Movie.PosterUrl,
                streamingHint = item.Movie.StreamingHint
            }),
            recentSessions = recentSessions.Select(item => new
            {
                detectedEmotion = item.DetectedEmotion,
                emotionSource = item.EmotionSource,
                genreFilters = item.GenreFilters,
                recommendedTitles = item.RecommendedTitles,
                createdUtc = item.CreatedUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm")
            })
        };
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }
}
