namespace ML_MOVIE.Models;

public sealed class FacialEmotionPredictionResult
{
    public EmotionType? Emotion { get; init; }
    public float Confidence { get; init; }
    public required string Source { get; init; }
}
