namespace ML_MOVIE.Models;

public sealed class EmotionOption
{
    public required EmotionType Emotion { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string AccentClass { get; init; }
}
