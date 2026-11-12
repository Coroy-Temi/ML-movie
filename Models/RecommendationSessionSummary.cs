namespace ML_MOVIE.Models;

public sealed class RecommendationSessionSummary
{
    public required string DetectedEmotion { get; init; }
    public required string EmotionSource { get; init; }
    public required string GenreFilters { get; init; }
    public required string RecommendedTitles { get; init; }
    public required DateTime CreatedUtc { get; init; }
}
