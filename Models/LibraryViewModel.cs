namespace ML_MOVIE.Models;

public sealed class LibraryViewModel
{
    public required string DisplayName { get; init; }
    public required IReadOnlyList<RecommendationSessionSummary> Sessions { get; init; }
}
