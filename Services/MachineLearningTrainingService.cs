using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.ML;
using ML_MOVIE.ML.Training;

namespace ML_MOVIE.Services;

public sealed class MachineLearningTrainingService : IMachineLearningTrainingService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MachineLearningTrainingService> _logger;
    private readonly MLContext _mlContext = new(seed: 42);
    private const int FacialVariantsPerProfile = 12;

    public MachineLearningTrainingService(
        IWebHostEnvironment environment,
        ILogger<MachineLearningTrainingService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task EnsureModelsAsync(CancellationToken cancellationToken = default)
    {
        var datasetDirectory = Path.Combine(_environment.ContentRootPath, "Data", "datasets");
        var modelDirectory = Path.Combine(_environment.ContentRootPath, "Data", "models");
        var cacheDirectory = Path.Combine(_environment.ContentRootPath, "Data", "training-cache");
        Directory.CreateDirectory(modelDirectory);
        Directory.CreateDirectory(cacheDirectory);

        var reports = new List<ModelTrainingReport>
        {
            await TrainMovieEmotionModelAsync(datasetDirectory, modelDirectory, cacheDirectory, cancellationToken),
            await TrainFacialEmotionModelAsync(datasetDirectory, modelDirectory, cacheDirectory, cancellationToken)
        };

        var summary = new TrainingSummaryReport
        {
            GeneratedUtc = DateTime.UtcNow,
            Models = reports
        };

        var summaryPath = Path.Combine(modelDirectory, "training-report.json");
        await File.WriteAllTextAsync(
            summaryPath,
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private async Task<ModelTrainingReport> TrainMovieEmotionModelAsync(
        string datasetDirectory,
        string modelDirectory,
        string cacheDirectory,
        CancellationToken cancellationToken)
    {
        var moviesPath = Path.Combine(datasetDirectory, "movies_catalog.csv");
        var emotionSignalsPath = Path.Combine(datasetDirectory, "emotion_signals.csv");
        var modelPath = Path.Combine(modelDirectory, "movie-emotion-model.zip");
        var cachePath = Path.Combine(cacheDirectory, "movie-emotion-training.csv");

        if (!File.Exists(moviesPath) || !File.Exists(emotionSignalsPath))
        {
            _logger.LogWarning("Movie emotion training skipped because the required datasets are missing.");
            return BuildSkippedReport("MovieEmotionModel", modelPath, cachePath, "Missing required datasets.");
        }

        await PrepareMovieEmotionCacheAsync(moviesPath, emotionSignalsPath, cachePath, cancellationToken);
        var totalRows = CountTrainingRows(cachePath);
        if (totalRows < 10)
        {
            _logger.LogWarning("Movie emotion training skipped because there are not enough rows.");
            return BuildSkippedReport("MovieEmotionModel", modelPath, cachePath, "Not enough training rows.", totalRows);
        }

        if (!ShouldRetrain(modelPath, cachePath))
        {
            return BuildSkippedReport("MovieEmotionModel", modelPath, cachePath, "Model already up to date.", totalRows);
        }

        var data = _mlContext.Data.LoadFromTextFile<MovieEmotionTrainingRow>(cachePath, hasHeader: true, separatorChar: ',');
        var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
            .Append(_mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(MovieEmotionTrainingRow.Title)))
            .Append(_mlContext.Transforms.Text.FeaturizeText("GenreFeatures", nameof(MovieEmotionTrainingRow.Genres)))
            .Append(_mlContext.Transforms.Text.FeaturizeText("OverviewFeatures", nameof(MovieEmotionTrainingRow.Overview)))
            .Append(_mlContext.Transforms.Text.FeaturizeText("TagFeatures", nameof(MovieEmotionTrainingRow.MoodTags)))
            .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeatures", "GenreFeatures", "OverviewFeatures", "TagFeatures"))
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(split.TrainSet);
        var predictions = model.Transform(split.TestSet);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);
        _mlContext.Model.Save(model, data.Schema, modelPath);

        _logger.LogInformation(
            "Trained movie emotion model with {Count} rows. MacroAccuracy={MacroAccuracy:0.000}, MicroAccuracy={MicroAccuracy:0.000}",
            totalRows,
            metrics.MacroAccuracy,
            metrics.MicroAccuracy);

        return new ModelTrainingReport
        {
            ModelName = "MovieEmotionModel",
            ModelPath = modelPath,
            TrainingCachePath = cachePath,
            TotalRows = totalRows,
            TrainingRows = EstimateTrainingRows(totalRows),
            TestRows = EstimateTestRows(totalRows),
            MacroAccuracy = metrics.MacroAccuracy,
            MicroAccuracy = metrics.MicroAccuracy,
            LogLoss = metrics.LogLoss,
            TrainedUtc = DateTime.UtcNow,
            Status = "Trained"
        };
    }

    private async Task<ModelTrainingReport> TrainFacialEmotionModelAsync(
        string datasetDirectory,
        string modelDirectory,
        string cacheDirectory,
        CancellationToken cancellationToken)
    {
        var samplesPath = Path.Combine(datasetDirectory, "facial_expression_samples.csv");
        var profilesPath = Path.Combine(datasetDirectory, "facial_landmark_profiles.csv");
        var modelPath = Path.Combine(modelDirectory, "facial-emotion-model.zip");
        var cachePath = Path.Combine(cacheDirectory, "facial-emotion-training-v2.csv");

        if (!File.Exists(samplesPath) || !File.Exists(profilesPath))
        {
            _logger.LogWarning("Facial emotion training skipped because the required datasets are missing.");
            return BuildSkippedReport("FacialEmotionModel", modelPath, cachePath, "Missing required datasets.");
        }

        await PrepareFacialEmotionCacheAsync(samplesPath, profilesPath, cachePath, cancellationToken);
        var totalRows = CountTrainingRows(cachePath);
        if (totalRows < 10)
        {
            _logger.LogWarning("Facial emotion training skipped because there are not enough rows.");
            return BuildSkippedReport("FacialEmotionModel", modelPath, cachePath, "Not enough training rows.", totalRows);
        }

        if (!ShouldRetrain(modelPath, cachePath))
        {
            return BuildSkippedReport("FacialEmotionModel", modelPath, cachePath, "Model already up to date.", totalRows);
        }

        var rows = LoadFacialTrainingRows(cachePath);
        var (trainingRows, testRows) = SplitFacialTrainingRows(rows);
        if (trainingRows.Count == 0 || testRows.Count == 0)
        {
            _logger.LogWarning("Facial emotion training skipped because the grouped split did not produce enough rows.");
            return BuildSkippedReport("FacialEmotionModel", modelPath, cachePath, "Grouped split did not produce enough train/test rows.", totalRows);
        }

        var trainData = _mlContext.Data.LoadFromEnumerable(trainingRows);
        var testData = _mlContext.Data.LoadFromEnumerable(testRows);

        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
            .Append(_mlContext.Transforms.Concatenate("Features",
                nameof(FacialEmotionTrainingRow.SmileScore),
                nameof(FacialEmotionTrainingRow.BrowTension),
                nameof(FacialEmotionTrainingRow.EyeOpenness),
                nameof(FacialEmotionTrainingRow.MouthWidthRatio),
                nameof(FacialEmotionTrainingRow.MouthHeightRatio),
                nameof(FacialEmotionTrainingRow.BrowRaiseRatio),
                nameof(FacialEmotionTrainingRow.EyeOpenRatio),
                nameof(FacialEmotionTrainingRow.CheekLiftRatio),
                nameof(FacialEmotionTrainingRow.JawDropRatio)))
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(trainData);
        var predictions = model.Transform(testData);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);
        _mlContext.Model.Save(model, trainData.Schema, modelPath);

        _logger.LogInformation(
            "Trained facial emotion model with {Count} rows. MacroAccuracy={MacroAccuracy:0.000}, MicroAccuracy={MicroAccuracy:0.000}",
            totalRows,
            metrics.MacroAccuracy,
            metrics.MicroAccuracy);

        return new ModelTrainingReport
        {
            ModelName = "FacialEmotionModel",
            ModelPath = modelPath,
            TrainingCachePath = cachePath,
            TotalRows = totalRows,
            TrainingRows = trainingRows.Count,
            TestRows = testRows.Count,
            MacroAccuracy = metrics.MacroAccuracy,
            MicroAccuracy = metrics.MicroAccuracy,
            LogLoss = metrics.LogLoss,
            TrainedUtc = DateTime.UtcNow,
            Status = "Trained"
        };
    }

    private async Task PrepareMovieEmotionCacheAsync(
        string moviesPath,
        string emotionSignalsPath,
        string cachePath,
        CancellationToken cancellationToken)
    {
        if (!ShouldRetrain(cachePath, moviesPath, emotionSignalsPath))
        {
            return;
        }

        var signalMap = LoadSignalLabels(emotionSignalsPath);
        await using var writer = new StreamWriter(cachePath, false, Encoding.UTF8);
        await writer.WriteLineAsync("Title,Genres,Overview,MoodTags,Label");

        foreach (var row in File.ReadLines(moviesPath).Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var columns = SplitCsv(row);
            if (columns.Count < 9 || !signalMap.TryGetValue(columns[0], out var label))
            {
                continue;
            }

            await writer.WriteLineAsync(string.Join(",",
                Csv(columns[1]),
                Csv(columns[2]),
                Csv(columns[3]),
                Csv(columns[8]),
                Csv(label)));
        }
    }

    private async Task PrepareFacialEmotionCacheAsync(
        string samplesPath,
        string profilesPath,
        string cachePath,
        CancellationToken cancellationToken)
    {
        if (!ShouldRetrain(cachePath, samplesPath, profilesPath))
        {
            return;
        }

        var profiles = new Dictionary<string, List<(float mouthWidth, float mouthHeight, float browRaise, float eyeOpen, float cheekLift, float jawDrop)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in File.ReadLines(profilesPath).Skip(1))
        {
            var columns = SplitCsv(row);
            if (columns.Count < 8)
            {
                continue;
            }

            if (!profiles.TryGetValue(columns[1], out var profileList))
            {
                profileList = [];
                profiles[columns[1]] = profileList;
            }

            profileList.Add((
                ParseFloat(columns[2]),
                ParseFloat(columns[3]),
                ParseFloat(columns[4]),
                ParseFloat(columns[5]),
                ParseFloat(columns[6]),
                ParseFloat(columns[7])));
        }

        await using var writer = new StreamWriter(cachePath, false, Encoding.UTF8);
        await writer.WriteLineAsync("SmileScore,BrowTension,EyeOpenness,MouthWidthRatio,MouthHeightRatio,BrowRaiseRatio,EyeOpenRatio,CheekLiftRatio,JawDropRatio,Label,SourceKey");

        foreach (var row in File.ReadLines(samplesPath).Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var columns = SplitCsv(row);
            if (columns.Count < 8 || !profiles.TryGetValue(columns[2], out var profileList))
            {
                continue;
            }

            var baseSmileScore = ParseFloat(columns[4]);
            var baseBrowTension = ParseFloat(columns[5]);
            var baseEyeOpenness = ParseFloat(columns[6]);

            foreach (var profile in profileList)
            {
                for (var variant = 0; variant < FacialVariantsPerProfile; variant++)
                {
                    var seed = $"{columns[0]}:{columns[1]}:{columns[2]}:{variant}";
                    var smileJitter = Jitter(seed, 0.035f);
                    var browJitter = Jitter(seed + ":brow", 0.04f);
                    var eyeJitter = Jitter(seed + ":eye", 0.04f);
                    var widthJitter = Jitter(seed + ":width", 0.03f);
                    var heightJitter = Jitter(seed + ":height", 0.03f);
                    var raiseJitter = Jitter(seed + ":raise", 0.035f);
                    var openJitter = Jitter(seed + ":open", 0.035f);
                    var cheekJitter = Jitter(seed + ":cheek", 0.035f);
                    var jawJitter = Jitter(seed + ":jaw", 0.03f);
                    var noiseLevel = NoiseLevel(columns[3]);
                    var asymmetry = Jitter(seed + ":asymmetry", 0.06f);
                    var occlusion = PositiveJitter(seed + ":occlusion", 0.07f) * noiseLevel;
                    var blur = PositiveJitter(seed + ":blur", 0.06f) * noiseLevel;
                    var headTilt = Jitter(seed + ":tilt", 0.05f) * (0.65f + noiseLevel);
                    var lightShift = Jitter(seed + ":light", 0.045f) * (0.55f + noiseLevel);
                    var confidence = ParseFloat(columns[3]);
                    var confidencePenalty = (1f - confidence) * 0.08f;
                    var sourceKey = $"{columns[0]}|{columns[2]}|{profile.mouthWidth:0.000}|{profile.eyeOpen:0.000}";

                    var smileScore = Clamp01(baseSmileScore + smileJitter - (occlusion * 0.25f) - confidencePenalty);
                    var browTension = Clamp01(baseBrowTension + browJitter + (occlusion * 0.18f) + (noiseLevel * 0.04f));
                    var eyeOpenness = Clamp01(baseEyeOpenness + eyeJitter - blur + lightShift);
                    var mouthWidth = Clamp01(profile.mouthWidth + widthJitter + (asymmetry * 0.65f) - (occlusion * 0.08f));
                    var mouthHeight = Clamp01(profile.mouthHeight + heightJitter + PositiveJitter(seed + ":mouth-open", 0.03f) - (blur * 0.04f));
                    var browRaise = Clamp01(profile.browRaise + raiseJitter + (headTilt * 0.30f) + lightShift);
                    var eyeOpen = Clamp01(profile.eyeOpen + openJitter - (blur * 0.45f) - (occlusion * 0.22f) - (asymmetry * 0.20f));
                    var cheekLift = Clamp01(profile.cheekLift + cheekJitter + (asymmetry * 0.35f) - (occlusion * 0.25f));
                    var jawDrop = Clamp01(profile.jawDrop + jawJitter + PositiveJitter(seed + ":jaw-open", 0.028f) + (headTilt * 0.18f) - (blur * 0.08f));

                    await writer.WriteLineAsync(string.Join(",",
                        smileScore.ToString(CultureInfo.InvariantCulture),
                        browTension.ToString(CultureInfo.InvariantCulture),
                        eyeOpenness.ToString(CultureInfo.InvariantCulture),
                        mouthWidth.ToString(CultureInfo.InvariantCulture),
                        mouthHeight.ToString(CultureInfo.InvariantCulture),
                        browRaise.ToString(CultureInfo.InvariantCulture),
                        eyeOpen.ToString(CultureInfo.InvariantCulture),
                        cheekLift.ToString(CultureInfo.InvariantCulture),
                        jawDrop.ToString(CultureInfo.InvariantCulture),
                        Csv(columns[2]),
                        Csv(sourceKey)));
                }
            }
        }
    }

    private static ModelTrainingReport BuildSkippedReport(
        string modelName,
        string modelPath,
        string cachePath,
        string status,
        int totalRows = 0)
    {
        return new ModelTrainingReport
        {
            ModelName = modelName,
            ModelPath = modelPath,
            TrainingCachePath = cachePath,
            TotalRows = totalRows,
            TrainingRows = 0,
            TestRows = 0,
            MacroAccuracy = 0,
            MicroAccuracy = 0,
            LogLoss = 0,
            TrainedUtc = DateTime.UtcNow,
            Status = status
        };
    }

    private static bool ShouldRetrain(string outputPath, params string[] sourcePaths)
    {
        if (!File.Exists(outputPath))
        {
            return true;
        }

        var outputWriteTime = File.GetLastWriteTimeUtc(outputPath);
        return sourcePaths.Any(path => File.Exists(path) && File.GetLastWriteTimeUtc(path) > outputWriteTime);
    }

    private static int CountTrainingRows(string path)
    {
        return File.Exists(path) ? Math.Max(0, File.ReadLines(path).Skip(1).Count()) : 0;
    }

    private static int EstimateTrainingRows(int totalRows)
    {
        return (int)Math.Round(totalRows * 0.8, MidpointRounding.AwayFromZero);
    }

    private static int EstimateTestRows(int totalRows)
    {
        return totalRows - EstimateTrainingRows(totalRows);
    }

    private static List<FacialEmotionTrainingRowWithSource> LoadFacialTrainingRows(string path)
    {
        var rows = new List<FacialEmotionTrainingRowWithSource>();
        foreach (var row in File.ReadLines(path).Skip(1))
        {
            var columns = SplitCsv(row);
            if (columns.Count < 11)
            {
                continue;
            }

            rows.Add(new FacialEmotionTrainingRowWithSource
            {
                SmileScore = ParseFloat(columns[0]),
                BrowTension = ParseFloat(columns[1]),
                EyeOpenness = ParseFloat(columns[2]),
                MouthWidthRatio = ParseFloat(columns[3]),
                MouthHeightRatio = ParseFloat(columns[4]),
                BrowRaiseRatio = ParseFloat(columns[5]),
                EyeOpenRatio = ParseFloat(columns[6]),
                CheekLiftRatio = ParseFloat(columns[7]),
                JawDropRatio = ParseFloat(columns[8]),
                Label = columns[9],
                SourceKey = columns[10]
            });
        }

        return rows;
    }

    private static (List<FacialEmotionTrainingRow> train, List<FacialEmotionTrainingRow> test) SplitFacialTrainingRows(
        IReadOnlyList<FacialEmotionTrainingRowWithSource> rows)
    {
        var grouped = rows
            .GroupBy(item => item.SourceKey)
            .OrderBy(group => StableHash(group.Key))
            .ToArray();

        var groupedByEmotion = grouped
            .GroupBy(group => group.First().Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var train = new List<FacialEmotionTrainingRow>();
        var test = new List<FacialEmotionTrainingRow>();

        foreach (var emotionGroup in groupedByEmotion)
        {
            var groups = emotionGroup.Value;
            var testGroupCount = Math.Max(1, (int)Math.Round(groups.Length * 0.2, MidpointRounding.AwayFromZero));

            foreach (var group in groups.Take(testGroupCount))
            {
                test.AddRange(group.Select(ToTrainingRow));
            }

            foreach (var group in groups.Skip(testGroupCount))
            {
                train.AddRange(group.Select(ToTrainingRow));
            }
        }

        return (train, test);
    }

    private static FacialEmotionTrainingRow ToTrainingRow(FacialEmotionTrainingRowWithSource row)
    {
        return new FacialEmotionTrainingRow
        {
            SmileScore = row.SmileScore,
            BrowTension = row.BrowTension,
            EyeOpenness = row.EyeOpenness,
            MouthWidthRatio = row.MouthWidthRatio,
            MouthHeightRatio = row.MouthHeightRatio,
            BrowRaiseRatio = row.BrowRaiseRatio,
            EyeOpenRatio = row.EyeOpenRatio,
            CheekLiftRatio = row.CheekLiftRatio,
            JawDropRatio = row.JawDropRatio,
            Label = row.Label
        };
    }

    private static Dictionary<string, string> LoadSignalLabels(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in File.ReadLines(path).Skip(1))
        {
            var columns = SplitCsv(row);
            if (columns.Count < 9)
            {
                continue;
            }

            var scores = new Dictionary<string, float>
            {
                ["Happy"] = ParseFloat(columns[1]),
                ["Sad"] = ParseFloat(columns[2]),
                ["Excited"] = ParseFloat(columns[3]),
                ["Relaxed"] = ParseFloat(columns[4]),
                ["Romantic"] = ParseFloat(columns[5]),
                ["Curious"] = ParseFloat(columns[6]),
                ["Fearless"] = ParseFloat(columns[7]),
                ["Inspired"] = ParseFloat(columns[8])
            };

            result[columns[0]] = scores.OrderByDescending(item => item.Value).First().Key;
        }

        return result;
    }

    private static List<string> SplitCsv(string row)
    {
        var columns = new List<string>();
        var builder = new StringBuilder();
        var insideQuotes = false;

        for (var i = 0; i < row.Length; i++)
        {
            var character = row[i];
            if (character == '"')
            {
                if (insideQuotes && i + 1 < row.Length && row[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (character == ',' && !insideQuotes)
            {
                columns.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        columns.Add(builder.ToString());
        return columns;
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static float ParseFloat(string? value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0f;
    }

    private static float Jitter(string seed, float amplitude)
    {
        var normalized = NormalizedSeed(seed);
        return (normalized - 0.5f) * amplitude * 2f;
    }

    private static float PositiveJitter(string seed, float amplitude)
    {
        return NormalizedSeed(seed) * amplitude;
    }

    private static float NoiseLevel(string? confidenceValue)
    {
        var confidence = ParseFloat(confidenceValue);
        return Math.Clamp(1f - confidence, 0.04f, 0.34f);
    }

    private static float NormalizedSeed(string seed)
    {
        var hash = StableHash(seed);
        return ((hash & 0x7fffffff) % 1000) / 999f;
    }

    private static int StableHash(string seed)
    {
        return seed.Aggregate(17, (current, character) => unchecked(current * 31 + character));
    }

    private static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    private sealed class FacialEmotionTrainingRowWithSource
    {
        public float SmileScore { get; init; }
        public float BrowTension { get; init; }
        public float EyeOpenness { get; init; }
        public float MouthWidthRatio { get; init; }
        public float MouthHeightRatio { get; init; }
        public float BrowRaiseRatio { get; init; }
        public float EyeOpenRatio { get; init; }
        public float CheekLiftRatio { get; init; }
        public float JawDropRatio { get; init; }
        public string Label { get; init; } = string.Empty;
        public string SourceKey { get; init; } = string.Empty;
    }
}
