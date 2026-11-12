namespace ML_MOVIE.Models;

public sealed class DashboardViewModel
{
    public required HomeViewModel Discovery { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<RecommendationSessionSummary> RecentSessions { get; init; }
    public required FacialEmotionInput CameraInput { get; init; }
    public required bool IsEmailVerified { get; init; }
}
