namespace ML_MOVIE.Models;

public sealed class Movie
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Overview { get; init; }
    public required string Genres { get; init; }
    public required string MoodTags { get; init; }
    public required int ReleaseYear { get; init; }
    public required double Rating { get; init; }
    public required int RuntimeMinutes { get; init; }
    public required string PosterUrl { get; init; }
    public required string StreamingHint { get; init; }
}
