namespace ML_MOVIE.ML.Training;

public sealed class TrainingSummaryReport
{
    public required DateTime GeneratedUtc { get; init; }
    public required IReadOnlyList<ModelTrainingReport> Models { get; init; }
}
