using System.Globalization;
using ML_MOVIE.Models;

namespace ML_MOVIE.Services;

public sealed class MovieRecommendationService : IMovieRecommendationService
{
    private static readonly IReadOnlyDictionary<EmotionType, string[]> EmotionKeywords = new Dictionary<EmotionType, string[]>
    {
        [EmotionType.Happy] = ["uplifting", "feel-good", "joyful", "fun", "light"],
        [EmotionType.Sad] = ["healing", "reflective", "comfort", "hopeful", "gentle"],
        [EmotionType.Excited] = ["adrenaline", "thrilling", "fast-paced", "epic", "intense"],
        [EmotionType.Relaxed] = ["calm", "slow-burn", "peaceful", "cozy", "easygoing"],
        [EmotionType.Romantic] = ["romantic", "tender", "chemistry", "heartfelt", "passionate"],
        [EmotionType.Curious] = ["mystery", "smart", "mind-bending", "investigation", "surprising"],
        [EmotionType.Fearless] = ["dark", "tense", "horror", "survival", "gritty"],
        [EmotionType.Inspired] = ["ambitious", "motivational", "triumph", "visionary", "dream"]
    };

    private static readonly IReadOnlyDictionary<EmotionType, string[]> EmotionGenreBoosts = new Dictionary<EmotionType, string[]>
    {
        [EmotionType.Happy] = ["Comedy", "Animation", "Family", "Adventure"],
        [EmotionType.Sad] = ["Drama", "Family", "Romance"],
        [EmotionType.Excited] = ["Action", "Adventure", "Science Fiction", "Thriller"],
        [EmotionType.Relaxed] = ["Animation", "Family", "Fantasy", "Documentary"],
        [EmotionType.Romantic] = ["Romance", "Drama", "Comedy"],
        [EmotionType.Curious] = ["Mystery", "Science Fiction", "Documentary", "Thriller"],
        [EmotionType.Fearless] = ["Horror", "Thriller", "Action"],
        [EmotionType.Inspired] = ["Drama", "Adventure", "History", "Documentary"]
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IEmotionInferenceService _emotionInferenceService;
    private readonly ILogger<MovieRecommendationService> _logger;
    private readonly Lazy<IReadOnlyList<Movie>> _movies;
    private readonly Lazy<IReadOnlyDictionary<string, Dictionary<EmotionType, double>>> _emotionSignals;
    private readonly IReadOnlyList<EmotionOption> _emotionOptions;

    public MovieRecommendationService(
        IWebHostEnvironment environment,
        ILogger<MovieRecommendationService> logger,
        IEmotionInferenceService emotionInferenceService)
    {
        _environment = environment;
        _logger = logger;
        _emotionInferenceService = emotionInferenceService;
        _movies = new Lazy<IReadOnlyList<Movie>>(LoadMovies);
        _emotionSignals = new Lazy<IReadOnlyDictionary<string, Dictionary<EmotionType, double>>>(LoadEmotionSignals);
        _emotionOptions =
        [
            new EmotionOption { Emotion = EmotionType.Happy, Title = "Happy", Subtitle = "Bright, easy wins and crowd-pleasers.", AccentClass = "sunrise" },
            new EmotionOption { Emotion = EmotionType.Sad, Title = "Sad", Subtitle = "Comforting stories with warmth and recovery.", AccentClass = "twilight" },
            new EmotionOption { Emotion = EmotionType.Excited, Title = "Excited", Subtitle = "High-energy picks with momentum.", AccentClass = "ember" },
            new EmotionOption { Emotion = EmotionType.Relaxed, Title = "Relaxed", Subtitle = "Low-stress picks for winding down.", AccentClass = "ocean" },
            new EmotionOption { Emotion = EmotionType.Romantic, Title = "Romantic", Subtitle = "Connection, chemistry, and charm.", AccentClass = "rose" },
            new EmotionOption { Emotion = EmotionType.Curious, Title = "Curious", Subtitle = "Smart mysteries and big ideas.", AccentClass = "aurora" },
            new EmotionOption { Emotion = EmotionType.Fearless, Title = "Fearless", Subtitle = "Dark rides for bold moods.", AccentClass = "midnight" },
            new EmotionOption { Emotion = EmotionType.Inspired, Title = "Inspired", Subtitle = "Ambitious stories that spark action.", AccentClass = "gold" }
        ];
    }

    public HomeViewModel BuildHomeViewModel(
        string? emotionInput,
        IEnumerable<string> selectedGenres,
        int count,
        IEnumerable<HomepageUpdateViewModel>? homepageUpdates = null)
    {
        var selectedEmotion = ParseEmotion(emotionInput);
        var cleanGenres = selectedGenres
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var recommendations = RecommendMovies(selectedEmotion, cleanGenres, Math.Clamp(count, 3, 12));
        var movies = _movies.Value;
        var emptyStateMessage = recommendations.Count == 0
            ? BuildEmptyStateMessage(selectedEmotion, cleanGenres)
            : string.Empty;

        return new HomeViewModel
        {
            Emotions = _emotionOptions,
            Recommendations = recommendations,
            Datasets = BuildDatasetSummaries(),
            AvailableGenres = movies
                .SelectMany(movie => SplitValues(movie.Genres))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(genre => genre)
                .ToArray(),
            SelectedGenres = cleanGenres,
            HomepageUpdates = homepageUpdates?.ToArray() ?? Array.Empty<HomepageUpdateViewModel>(),
            SelectedEmotion = selectedEmotion,
            MovieCount = recommendations.Count,
            CatalogSize = movies.Count,
            EmptyStateMessage = emptyStateMessage
        };
    }

    public IReadOnlyList<EmotionOption> GetEmotionOptions() => _emotionOptions;

    public IReadOnlyList<MovieRecommendation> GetPreviewRecommendations()
    {
        return RecommendMovies(EmotionType.Happy, Array.Empty<string>(), 6);
    }

    public IReadOnlyList<DatasetSummary> GetDatasetSummaries() => BuildDatasetSummaries();

    public IReadOnlyList<string> GetAvailableGenres()
    {
        return _movies.Value
            .SelectMany(movie => SplitValues(movie.Genres))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(genre => genre)
            .ToArray();
    }

    public int GetCatalogSize() => _movies.Value.Count;

    private IReadOnlyList<MovieRecommendation> RecommendMovies(EmotionType emotion, IReadOnlyCollection<string> selectedGenres, int count)
    {
        var emotionSignals = _emotionSignals.Value;
        var pool = _movies.Value
            .Where(movie => MatchesSelectedGenres(movie, selectedGenres))
            .ToArray();

        if (pool.Length == 0)
        {
            return Array.Empty<MovieRecommendation>();
        }

        var ranked = pool
            .Select(movie =>
            {
                var score = movie.Rating * 1.8;
                score += ScoreKeywordMatches(movie, emotion);
                score += ScoreGenreMatches(movie, emotion, selectedGenres);
                score += ScoreExternalEmotionSignal(movie.Id, emotionSignals, emotion);

                return new MovieRecommendation
                {
                    Movie = movie,
                    Score = score,
                    Reason = BuildReason(movie, emotion, selectedGenres)
                };
            })
            .ToArray();

        var strictMatches = ranked
            .Where(item => IsEmotionAligned(item.Movie, emotion, emotionSignals))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Movie.Rating)
            .Take(count)
            .ToArray();

        if (strictMatches.Length >= Math.Min(4, count))
        {
            return strictMatches;
        }

        if (selectedGenres.Count > 0 && strictMatches.Length == 0)
        {
            return Array.Empty<MovieRecommendation>();
        }

        var genreAligned = ranked
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Movie.Rating)
            .Take(count)
            .ToArray();

        return genreAligned
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Movie.Rating)
            .Take(count)
            .ToArray();
    }

    private static string BuildEmptyStateMessage(EmotionType emotion, IReadOnlyList<string> selectedGenres)
    {
        if (selectedGenres.Count == 0)
        {
            return $"Such movies or series are not available yet for the {emotion.ToString().ToLowerInvariant()} mood.";
        }

        var genreList = string.Join(", ", selectedGenres);
        return $"Such movies or series are not available yet for the {emotion.ToString().ToLowerInvariant()} mood with {genreList}.";
    }

    private static bool MatchesSelectedGenres(Movie movie, IReadOnlyCollection<string> selectedGenres)
    {
        if (selectedGenres.Count == 0)
        {
            return true;
        }

        var genres = SplitValues(movie.Genres);
        return selectedGenres.All(selected => genres.Contains(selected, StringComparer.OrdinalIgnoreCase));
    }

    private double ScoreKeywordMatches(Movie movie, EmotionType emotion)
    {
        var score = 0d;
        var searchable = $"{movie.Overview} {movie.MoodTags}".ToLowerInvariant();

        foreach (var keyword in EmotionKeywords[emotion])
        {
            if (searchable.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 2.2;
            }
        }

        return score;
    }

    private double ScoreGenreMatches(Movie movie, EmotionType emotion, IReadOnlyCollection<string> selectedGenres)
    {
        var score = 0d;
        var genres = SplitValues(movie.Genres);

        foreach (var genre in EmotionGenreBoosts[emotion])
        {
            if (genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
            {
                score += 1.6;
            }
        }

        foreach (var genre in selectedGenres)
        {
            if (genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
            {
                score += 3.4;
            }
        }

        return score;
    }

    private static double ScoreExternalEmotionSignal(
        string movieId,
        IReadOnlyDictionary<string, Dictionary<EmotionType, double>> emotionSignals,
        EmotionType emotion)
    {
        if (!emotionSignals.TryGetValue(movieId, out var signalMap))
        {
            return 0;
        }

        return signalMap.TryGetValue(emotion, out var signal) ? signal * 5 : 0;
    }

    private bool IsEmotionAligned(
        Movie movie,
        EmotionType selectedEmotion,
        IReadOnlyDictionary<string, Dictionary<EmotionType, double>> emotionSignals)
    {
        var modelPrediction = _emotionInferenceService.PredictMovieEmotion(movie.Title, movie.Genres, movie.Overview, movie.MoodTags);
        if (modelPrediction is not null)
        {
            return modelPrediction == selectedEmotion;
        }

        if (emotionSignals.TryGetValue(movie.Id, out var signalMap) && signalMap.Count > 0)
        {
            var dominantEmotion = signalMap
                .OrderByDescending(item => item.Value)
                .First()
                .Key;

            return dominantEmotion == selectedEmotion;
        }

        var searchable = $"{movie.Overview} {movie.MoodTags}";
        return EmotionKeywords[selectedEmotion]
            .Any(keyword => searchable.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildReason(Movie movie, EmotionType emotion, IReadOnlyCollection<string> selectedGenres)
    {
        var genres = SplitValues(movie.Genres);
        var matchingSelectedGenre = selectedGenres.FirstOrDefault(genre => genres.Contains(genre, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matchingSelectedGenre))
        {
            return $"Matches your {matchingSelectedGenre} filter and the {emotion} mood profile.";
        }

        var matchingEmotionGenre = EmotionGenreBoosts[emotion].FirstOrDefault(genre => genres.Contains(genre, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matchingEmotionGenre))
        {
            return $"Leans into {matchingEmotionGenre} energy, which usually fits a {emotion.ToString().ToLowerInvariant()} mood.";
        }

        return $"Scored highly for {emotion.ToString().ToLowerInvariant()} viewers based on mood tags, overview keywords, and rating.";
    }

    private IReadOnlyList<DatasetSummary> BuildDatasetSummaries()
    {
        var moviesPath = GetDatasetPath("movies_catalog.csv");
        var emotionPath = GetDatasetPath("emotion_signals.csv");
        var facialReferencePath = GetDatasetPath("facial_expression_samples.csv");
        var facialLandmarksPath = GetDatasetPath("facial_landmark_profiles.csv");
        var movies = _movies.Value;
        var signals = _emotionSignals.Value;

        return
        [
            new DatasetSummary
            {
                Name = "Movie Metadata",
                FileName = Path.GetFileName(moviesPath),
                Description = "Main catalog with title, genres, ratings, runtime, poster, and synopsis.",
                IsLoaded = System.IO.File.Exists(moviesPath),
                RowCount = movies.Count
            },
            new DatasetSummary
            {
                Name = "Emotion Signals",
                FileName = Path.GetFileName(emotionPath),
                Description = "Per-movie emotional scores used to tune the recommendation engine.",
                IsLoaded = System.IO.File.Exists(emotionPath),
                RowCount = signals.Count
            },
            new DatasetSummary
            {
                Name = "Facial Expression Samples",
                FileName = Path.GetFileName(facialReferencePath),
                Description = "Reference dataset for mapping observed expressions such as smiles or frowns to mood classes.",
                IsLoaded = System.IO.File.Exists(facialReferencePath),
                RowCount = CountRows(facialReferencePath)
            },
            new DatasetSummary
            {
                Name = "Landmark Profiles",
                FileName = Path.GetFileName(facialLandmarksPath),
                Description = "Facial landmark ratios and expression features that support automatic mood detection.",
                IsLoaded = System.IO.File.Exists(facialLandmarksPath),
                RowCount = CountRows(facialLandmarksPath)
            }
        ];
    }

    private IReadOnlyList<Movie> LoadMovies()
    {
        var path = GetDatasetPath("movies_catalog.csv");
        if (!System.IO.File.Exists(path))
        {
            _logger.LogWarning("Movie dataset not found at {Path}. Using built-in starter catalog.", path);
            return GetSeedMovies();
        }

        var rows = System.IO.File.ReadLines(path).Skip(1);
        var movies = new List<Movie>();

        foreach (var row in rows)
        {
            var columns = ParseCsvLine(row);
            if (columns.Count < 9)
            {
                continue;
            }

            movies.Add(new Movie
            {
                Id = columns[0],
                Title = columns[1],
                Genres = columns[2],
                Overview = columns[3],
                ReleaseYear = ParseInt(columns[4], 2024),
                Rating = ParseDouble(columns[5], 7.0),
                RuntimeMinutes = ParseInt(columns[6], 100),
                PosterUrl = columns[7],
                MoodTags = columns[8],
                StreamingHint = columns.Count > 9 ? columns[9] : "Catalog source"
            });
        }

        return movies.Count > 0 ? movies : GetSeedMovies();
    }

    private IReadOnlyDictionary<string, Dictionary<EmotionType, double>> LoadEmotionSignals()
    {
        var path = GetDatasetPath("emotion_signals.csv");
        if (!System.IO.File.Exists(path))
        {
            _logger.LogWarning("Emotion signal dataset not found at {Path}. Using starter emotion profiles.", path);
            return BuildSeedEmotionSignals();
        }

        var signals = new Dictionary<string, Dictionary<EmotionType, double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in System.IO.File.ReadLines(path).Skip(1))
        {
            var columns = ParseCsvLine(row);
            if (columns.Count < 9)
            {
                continue;
            }

            signals[columns[0]] = new Dictionary<EmotionType, double>
            {
                [EmotionType.Happy] = ParseDouble(columns[1], 0.5),
                [EmotionType.Sad] = ParseDouble(columns[2], 0.5),
                [EmotionType.Excited] = ParseDouble(columns[3], 0.5),
                [EmotionType.Relaxed] = ParseDouble(columns[4], 0.5),
                [EmotionType.Romantic] = ParseDouble(columns[5], 0.5),
                [EmotionType.Curious] = ParseDouble(columns[6], 0.5),
                [EmotionType.Fearless] = ParseDouble(columns[7], 0.5),
                [EmotionType.Inspired] = ParseDouble(columns[8], 0.5)
            };
        }

        return signals.Count > 0 ? signals : BuildSeedEmotionSignals();
    }

    private string GetDatasetPath(string fileName)
    {
        return Path.Combine(_environment.ContentRootPath, "Data", "datasets", fileName);
    }

    private static EmotionType ParseEmotion(string? input)
    {
        return Enum.TryParse<EmotionType>(input, true, out var emotion)
            ? emotion
            : EmotionType.Happy;
    }

    private static int ParseInt(string? input, int fallback)
    {
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double ParseDouble(string? input, double fallback)
    {
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static IReadOnlyList<string> SplitValues(string rawValue)
    {
        return rawValue
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .ToArray();
    }

    private static int CountRows(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            return 0;
        }

        return Math.Max(0, System.IO.File.ReadLines(path).Skip(1).Count());
    }

    private static List<string> ParseCsvLine(string row)
    {
        var columns = new List<string>();
        var builder = new System.Text.StringBuilder();
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

    private static IReadOnlyList<Movie> GetSeedMovies()
    {
        return
        [
            new Movie
            {
                Id = "M001",
                Title = "The Grand Budapest Hotel",
                Genres = "Comedy|Adventure|Drama",
                Overview = "A stylish concierge and his lobby boy chase a stolen painting through a joyful, fast-moving caper.",
                ReleaseYear = 2014,
                Rating = 8.1,
                RuntimeMinutes = 99,
                PosterUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
                MoodTags = "uplifting|fun|quirky|adventure",
                StreamingHint = "Works well for evening group watchlists"
            },
            new Movie
            {
                Id = "M002",
                Title = "La La Land",
                Genres = "Romance|Drama|Music",
                Overview = "Two artists fall in love while chasing impossible dreams in a bright modern musical.",
                ReleaseYear = 2016,
                Rating = 8.0,
                RuntimeMinutes = 128,
                PosterUrl = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=900&q=80",
                MoodTags = "romantic|dream|heartfelt|inspiring",
                StreamingHint = "Strong pick for date-night recommendations"
            },
            new Movie
            {
                Id = "M003",
                Title = "Spider-Man: Into the Spider-Verse",
                Genres = "Animation|Action|Adventure",
                Overview = "Miles Morales discovers a multiverse of heroes in an energetic story packed with humor and momentum.",
                ReleaseYear = 2018,
                Rating = 8.4,
                RuntimeMinutes = 117,
                PosterUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=900&q=80",
                MoodTags = "adrenaline|joyful|inspiring|epic",
                StreamingHint = "Great for family and excited moods"
            },
            new Movie
            {
                Id = "M004",
                Title = "Arrival",
                Genres = "Science Fiction|Drama|Mystery",
                Overview = "A linguist decodes an alien language in a meditative first-contact mystery about grief and time.",
                ReleaseYear = 2016,
                Rating = 7.9,
                RuntimeMinutes = 116,
                PosterUrl = "https://images.unsplash.com/photo-1440404653325-ab127d49abc1?auto=format&fit=crop&w=900&q=80",
                MoodTags = "mind-bending|reflective|smart|mystery",
                StreamingHint = "Best for thoughtful solo sessions"
            },
            new Movie
            {
                Id = "M005",
                Title = "Paddington 2",
                Genres = "Family|Comedy|Adventure",
                Overview = "A bear spreads kindness across London in a warm, funny story with almost impossible charm.",
                ReleaseYear = 2017,
                Rating = 8.2,
                RuntimeMinutes = 103,
                PosterUrl = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=900&q=80",
                MoodTags = "feel-good|cozy|joyful|comfort",
                StreamingHint = "Ideal for happy or relaxed moods"
            },
            new Movie
            {
                Id = "M006",
                Title = "Top Gun: Maverick",
                Genres = "Action|Drama",
                Overview = "Veteran pilots take on a dangerous mission with speed, confidence, and a giant-screen attitude.",
                ReleaseYear = 2022,
                Rating = 8.3,
                RuntimeMinutes = 131,
                PosterUrl = "https://images.unsplash.com/photo-1513104890138-7c749659a591?auto=format&fit=crop&w=900&q=80",
                MoodTags = "thrilling|epic|adrenaline|triumph",
                StreamingHint = "Excellent for high-energy recommendations"
            },
            new Movie
            {
                Id = "M007",
                Title = "Past Lives",
                Genres = "Romance|Drama",
                Overview = "Two childhood friends reconnect years later in a soft, intimate story about timing and identity.",
                ReleaseYear = 2023,
                Rating = 7.8,
                RuntimeMinutes = 106,
                PosterUrl = "https://images.unsplash.com/photo-1502139214982-d0ad755818d8?auto=format&fit=crop&w=900&q=80",
                MoodTags = "tender|reflective|romantic|gentle",
                StreamingHint = "Ideal for quieter emotional recommendations"
            },
            new Movie
            {
                Id = "M008",
                Title = "Get Out",
                Genres = "Horror|Thriller|Mystery",
                Overview = "A weekend visit turns into a smart, tense survival thriller loaded with social commentary.",
                ReleaseYear = 2017,
                Rating = 7.8,
                RuntimeMinutes = 104,
                PosterUrl = "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=900&q=80",
                MoodTags = "dark|tense|survival|surprising",
                StreamingHint = "Best for fearless late-night moods"
            }
        ];
    }

    private static IReadOnlyDictionary<string, Dictionary<EmotionType, double>> BuildSeedEmotionSignals()
    {
        return new Dictionary<string, Dictionary<EmotionType, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["M001"] = BuildSignal(0.95, 0.40, 0.75, 0.50, 0.35, 0.55, 0.15, 0.65),
            ["M002"] = BuildSignal(0.70, 0.85, 0.45, 0.40, 0.98, 0.50, 0.10, 0.80),
            ["M003"] = BuildSignal(0.88, 0.25, 0.97, 0.35, 0.22, 0.60, 0.30, 0.92),
            ["M004"] = BuildSignal(0.30, 0.82, 0.52, 0.60, 0.25, 0.98, 0.25, 0.84),
            ["M005"] = BuildSignal(0.99, 0.72, 0.48, 0.90, 0.40, 0.33, 0.05, 0.68),
            ["M006"] = BuildSignal(0.72, 0.20, 0.99, 0.18, 0.26, 0.34, 0.70, 0.95),
            ["M007"] = BuildSignal(0.40, 0.94, 0.18, 0.66, 0.96, 0.70, 0.08, 0.72),
            ["M008"] = BuildSignal(0.12, 0.28, 0.82, 0.05, 0.08, 0.80, 0.99, 0.42)
        };
    }

    private static Dictionary<EmotionType, double> BuildSignal(
        double happy,
        double sad,
        double excited,
        double relaxed,
        double romantic,
        double curious,
        double fearless,
        double inspired)
    {
        return new Dictionary<EmotionType, double>
        {
            [EmotionType.Happy] = happy,
            [EmotionType.Sad] = sad,
            [EmotionType.Excited] = excited,
            [EmotionType.Relaxed] = relaxed,
            [EmotionType.Romantic] = romantic,
            [EmotionType.Curious] = curious,
            [EmotionType.Fearless] = fearless,
            [EmotionType.Inspired] = inspired
        };
    }
}
