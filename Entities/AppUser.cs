namespace ML_MOVIE.Entities;

public sealed class AppUser
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsEmailVerified { get; set; }
    public bool IsAdmin { get; set; }
    public string ThemePreference { get; set; } = "dark";
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime? VerificationCodeExpiresUtc { get; set; }
    public string PendingEmail { get; set; } = string.Empty;
    public ICollection<RecommendationSession> RecommendationSessions { get; set; } = new List<RecommendationSession>();
}
