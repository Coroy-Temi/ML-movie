namespace ML_MOVIE.Models;

public sealed class HomeViewModel
{
    public required IReadOnlyList<EmotionOption> Emotions { get; init; }
    public required IReadOnlyList<MovieRecommendation> Recommendations { get; init; }
    public required IReadOnlyList<DatasetSummary> Datasets { get; init; }
    public required IReadOnlyList<string> AvailableGenres { get; init; }
    public required IReadOnlyList<string> SelectedGenres { get; init; }
    public IReadOnlyList<HomepageUpdateViewModel> HomepageUpdates { get; init; } = Array.Empty<HomepageUpdateViewModel>();
    public required EmotionType SelectedEmotion { get; init; }
    public required int MovieCount { get; init; }
    public required int CatalogSize { get; init; }
    public string EmptyStateMessage { get; init; } = string.Empty;
}
