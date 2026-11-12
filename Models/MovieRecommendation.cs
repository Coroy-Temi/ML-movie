namespace ML_MOVIE.Models;

public sealed class MovieRecommendation
{
    public required Movie Movie { get; init; }
    public required double Score { get; init; }
    public required string Reason { get; init; }
}
