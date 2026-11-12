namespace ML_MOVIE.Entities;

public sealed class RecommendationSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string DetectedEmotion { get; set; }
    public required string EmotionSource { get; set; }
    public string GenreFilters { get; set; } = string.Empty;
    public string RecommendedTitles { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string CaptureDataUrl { get; set; } = string.Empty;

    public AppUser? User { get; set; }
}
