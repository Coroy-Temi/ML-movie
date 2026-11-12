namespace ML_MOVIE.Models;

public sealed class FacialEmotionApiResponse
{
    public required string Emotion { get; init; }
    public required string Source { get; init; }
    public float Confidence { get; init; }
}
