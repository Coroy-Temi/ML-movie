using Microsoft.ML;
using ML_MOVIE.ML.Inference;
using ML_MOVIE.ML.Training;
using ML_MOVIE.Models;

namespace ML_MOVIE.Services;

public sealed class EmotionInferenceService : IEmotionInferenceService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EmotionInferenceService> _logger;
    private readonly MLContext _mlContext = new(seed: 42);
    private PredictionEngine<MovieEmotionTrainingRow, MovieEmotionPrediction>? _moviePredictionEngine;
    private PredictionEngine<FacialEmotionInputFeatures, FacialEmotionPrediction>? _facialPredictionEngine;

    public EmotionInferenceService(
        IWebHostEnvironment environment,
        ILogger<EmotionInferenceService> logger)
    {
        _environment = environment;
        _logger = logger;
        LoadModels();
    }

    public EmotionType? PredictMovieEmotion(string title, string genres, string overview, string moodTags)
    {
        if (_moviePredictionEngine is null)
        {
            return null;
        }

        try
        {
            var prediction = _moviePredictionEngine.Predict(new MovieEmotionTrainingRow
            {
                Title = title,
                Genres = genres,
                Overview = overview,
                MoodTags = moodTags
            });

            return Enum.TryParse<EmotionType>(prediction.PredictedLabel, true, out var emotion)
                ? emotion
                : null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Movie emotion prediction failed.");
            return null;
        }
    }

    public EmotionType? PredictFacialEmotion(FacialEmotionInputFeatures input)
    {
        return PredictFacialEmotionDetailed(input).Emotion;
    }

    public FacialEmotionPredictionResult PredictFacialEmotionDetailed(FacialEmotionInputFeatures input)
    {
        var heuristic = PredictHeuristicEmotion(input);
        var mlEmotion = PredictMlEmotion(input);

        if (heuristic.emotion is not null && mlEmotion is null)
        {
            return new FacialEmotionPredictionResult
            {
                Emotion = heuristic.emotion,
                Confidence = heuristic.confidence,
                Source = $"Heuristic facial analysis ({heuristic.reason})"
            };
        }

        if (heuristic.emotion is not null && mlEmotion is not null)
        {
            if (heuristic.emotion == mlEmotion)
            {
                return new FacialEmotionPredictionResult
                {
                    Emotion = mlEmotion,
                    Confidence = MathF.Min(0.99f, 0.55f + heuristic.confidence * 0.4f),
                    Source = $"Trained ML model with facial landmark confirmation ({heuristic.reason})"
                };
            }

            if (heuristic.confidence >= 0.74f && (heuristic.emotion == EmotionType.Happy || heuristic.emotion == EmotionType.Sad || heuristic.emotion == EmotionType.Excited))
            {
                return new FacialEmotionPredictionResult
                {
                    Emotion = heuristic.emotion,
                    Confidence = heuristic.confidence,
                    Source = $"Heuristic override from mouth, cheek, and eye movement ({heuristic.reason})"
                };
            }

            return new FacialEmotionPredictionResult
            {
                Emotion = mlEmotion,
                Confidence = 0.62f,
                Source = "Trained ML facial emotion model"
            };
        }

        if (mlEmotion is not null)
        {
            return new FacialEmotionPredictionResult
            {
                Emotion = mlEmotion,
                Confidence = 0.6f,
                Source = "Trained ML facial emotion model"
            };
        }

        return new FacialEmotionPredictionResult
        {
            Emotion = heuristic.emotion,
            Confidence = heuristic.confidence,
            Source = heuristic.emotion is null
                ? "Fallback prediction"
                : $"Heuristic facial analysis ({heuristic.reason})"
        };
    }

    private EmotionType? PredictMlEmotion(FacialEmotionInputFeatures input)
    {
        if (_facialPredictionEngine is null)
        {
            return null;
        }

        try
        {
            var prediction = _facialPredictionEngine.Predict(input);
            return Enum.TryParse<EmotionType>(prediction.PredictedLabel, true, out var emotion)
                ? emotion
                : null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Facial emotion prediction failed.");
            return null;
        }
    }

    private static (EmotionType? emotion, float confidence, string reason) PredictHeuristicEmotion(FacialEmotionInputFeatures input)
    {
        var smileSignal = (input.SmileScore * 0.34f) + (input.CheekLiftRatio * 0.28f) + (input.MouthWidthRatio * 0.22f) + (input.EyeOpenRatio * 0.08f) + (1f - input.BrowTension) * 0.08f;
        var sadSignal = (input.BrowTension * 0.30f) + ((1f - input.EyeOpenRatio) * 0.22f) + ((1f - input.CheekLiftRatio) * 0.18f) + ((1f - input.MouthHeightRatio) * 0.18f) + ((1f - input.SmileScore) * 0.12f);
        var excitedSignal = (input.EyeOpenness * 0.28f) + (input.JawDropRatio * 0.24f) + (input.BrowRaiseRatio * 0.18f) + (input.SmileScore * 0.16f) + (input.EyeOpenRatio * 0.14f);
        var relaxedSignal = ((1f - input.BrowTension) * 0.30f) + (1f - MathF.Abs(input.EyeOpenRatio - 0.48f)) * 0.25f + ((1f - input.JawDropRatio) * 0.20f) + ((1f - MathF.Abs(input.SmileScore - 0.42f)) * 0.15f) + (input.CheekLiftRatio * 0.10f);
        var romanticSignal = (input.SmileScore * 0.26f) + (input.CheekLiftRatio * 0.22f) + ((1f - input.JawDropRatio) * 0.18f) + ((1f - input.BrowTension) * 0.14f) + (input.MouthHeightRatio * 0.10f) + (input.EyeOpenRatio * 0.10f);
        var curiousSignal = (input.BrowRaiseRatio * 0.28f) + (input.EyeOpenRatio * 0.22f) + ((1f - input.SmileScore) * 0.16f) + ((1f - input.CheekLiftRatio) * 0.12f) + (input.MouthHeightRatio * 0.12f) + ((1f - input.BrowTension) * 0.10f);
        var fearlessSignal = (input.BrowTension * 0.28f) + (input.JawDropRatio * 0.16f) + ((1f - input.SmileScore) * 0.16f) + (input.EyeOpenRatio * 0.14f) + ((1f - input.CheekLiftRatio) * 0.14f) + (input.MouthWidthRatio * 0.12f);
        var inspiredSignal = (input.BrowRaiseRatio * 0.20f) + (input.EyeOpenRatio * 0.18f) + (input.CheekLiftRatio * 0.18f) + (input.SmileScore * 0.16f) + ((1f - input.BrowTension) * 0.14f) + (input.MouthHeightRatio * 0.14f);

        if (smileSignal >= 0.66f && input.CheekLiftRatio >= 0.48f && input.MouthWidthRatio >= 0.45f)
        {
            return (EmotionType.Happy, MathF.Min(0.96f, smileSignal), "smile width and cheek lift");
        }

        if (sadSignal >= 0.62f && input.EyeOpenRatio <= 0.42f && input.BrowTension >= 0.42f)
        {
            return (EmotionType.Sad, MathF.Min(0.93f, sadSignal), "eye shrink and brow tension");
        }

        if (excitedSignal >= 0.64f && input.JawDropRatio >= 0.26f && input.EyeOpenRatio >= 0.55f)
        {
            return (EmotionType.Excited, MathF.Min(0.95f, excitedSignal), "jaw drop and widened eyes");
        }

        var ranked = new Dictionary<EmotionType, float>
        {
            [EmotionType.Happy] = smileSignal,
            [EmotionType.Sad] = sadSignal,
            [EmotionType.Excited] = excitedSignal,
            [EmotionType.Relaxed] = relaxedSignal,
            [EmotionType.Romantic] = romanticSignal,
            [EmotionType.Curious] = curiousSignal,
            [EmotionType.Fearless] = fearlessSignal,
            [EmotionType.Inspired] = inspiredSignal
        }
        .OrderByDescending(item => item.Value)
        .ToArray();

        var best = ranked[0];
        var second = ranked[1];
        if (best.Value < 0.54f || best.Value - second.Value < 0.05f)
        {
            return (null, 0.35f, "low-confidence facial pattern");
        }

        var reason = best.Key switch
        {
            EmotionType.Happy => "smile width and cheek lift",
            EmotionType.Sad => "eye shrink and brow tension",
            EmotionType.Excited => "jaw drop and widened eyes",
            EmotionType.Relaxed => "low tension and balanced eye openness",
            EmotionType.Romantic => "soft smile and calm jaw posture",
            EmotionType.Curious => "raised brow and focused eyes",
            EmotionType.Fearless => "tension with steady gaze",
            EmotionType.Inspired => "lifted brow and bright eyes",
            _ => "facial landmark pattern"
        };

        return (best.Key, MathF.Min(0.9f, best.Value), reason);
    }

    private void LoadModels()
    {
        var modelDirectory = Path.Combine(_environment.ContentRootPath, "Data", "models");
        var movieModelPath = Path.Combine(modelDirectory, "movie-emotion-model.zip");
        var facialModelPath = Path.Combine(modelDirectory, "facial-emotion-model.zip");

        if (File.Exists(movieModelPath))
        {
            var movieModel = _mlContext.Model.Load(movieModelPath, out _);
            _moviePredictionEngine = _mlContext.Model.CreatePredictionEngine<MovieEmotionTrainingRow, MovieEmotionPrediction>(movieModel);
        }

        if (File.Exists(facialModelPath))
        {
            var facialModel = _mlContext.Model.Load(facialModelPath, out _);
            _facialPredictionEngine = _mlContext.Model.CreatePredictionEngine<FacialEmotionInputFeatures, FacialEmotionPrediction>(facialModel);
        }
    }
}
