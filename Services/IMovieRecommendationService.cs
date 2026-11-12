using ML_MOVIE.Models;

namespace ML_MOVIE.Services;

public interface IMovieRecommendationService
{
    HomeViewModel BuildHomeViewModel(string? emotionInput, IEnumerable<string> selectedGenres, int count, IEnumerable<HomepageUpdateViewModel>? homepageUpdates = null);
    IReadOnlyList<EmotionOption> GetEmotionOptions();
    IReadOnlyList<MovieRecommendation> GetPreviewRecommendations();
    IReadOnlyList<DatasetSummary> GetDatasetSummaries();
    IReadOnlyList<string> GetAvailableGenres();
    int GetCatalogSize();
}
