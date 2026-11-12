namespace ML_MOVIE.Services;

public interface IMachineLearningTrainingService
{
    Task EnsureModelsAsync(CancellationToken cancellationToken = default);
}
