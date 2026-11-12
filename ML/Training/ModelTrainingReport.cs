namespace ML_MOVIE.ML.Training;

public sealed class ModelTrainingReport
{
    public required string ModelName { get; init; }
    public required string ModelPath { get; init; }
    public required string TrainingCachePath { get; init; }
    public required int TotalRows { get; init; }
    public required int TrainingRows { get; init; }
    public required int TestRows { get; init; }
    public required double MacroAccuracy { get; init; }
    public required double MicroAccuracy { get; init; }
    public required double LogLoss { get; init; }
    public required DateTime TrainedUtc { get; init; }
    public required string Status { get; init; }
}
