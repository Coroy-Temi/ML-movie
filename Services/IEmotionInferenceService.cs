using ML_MOVIE.ML.Inference;
using ML_MOVIE.Models;

namespace ML_MOVIE.Services;

public interface IEmotionInferenceService
{
    EmotionType? PredictMovieEmotion(string title, string genres, string overview, string moodTags);
    EmotionType? PredictFacialEmotion(FacialEmotionInputFeatures input);
    FacialEmotionPredictionResult PredictFacialEmotionDetailed(FacialEmotionInputFeatures input);
}
