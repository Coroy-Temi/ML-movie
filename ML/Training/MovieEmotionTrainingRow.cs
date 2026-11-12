using Microsoft.ML.Data;

namespace ML_MOVIE.ML.Training;

public sealed class MovieEmotionTrainingRow
{
    [LoadColumn(0)]
    public string Title { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string Genres { get; set; } = string.Empty;

    [LoadColumn(2)]
    public string Overview { get; set; } = string.Empty;

    [LoadColumn(3)]
    public string MoodTags { get; set; } = string.Empty;

    [LoadColumn(4)]
    public string Label { get; set; } = string.Empty;
}
