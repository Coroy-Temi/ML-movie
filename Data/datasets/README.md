# Dataset layout

This prototype expects two CSV datasets:

1. `movies_catalog.csv`
2. `emotion_signals.csv`
3. `facial_expression_samples.csv`
4. `facial_landmark_profiles.csv`

## `movies_catalog.csv`

Required columns:

- `id`
- `title`
- `genres`
- `overview`
- `releaseYear`
- `rating`
- `runtimeMinutes`
- `posterUrl`
- `moodTags`
- `streamingHint`

Suggested large-source replacements:

- MovieLens or TMDB export for the movie metadata catalog
- IMDb or a custom NLP-enriched export for additional synopsis and mood features

## `emotion_signals.csv`

Required columns:

- `id`
- `happy`
- `sad`
- `excited`
- `relaxed`
- `romantic`
- `curious`
- `fearless`
- `inspired`

Each value should be between `0.0` and `1.0`.

## `facial_expression_samples.csv`

Suggested columns:

- `sampleId`
- `expressionLabel`
- `dominantEmotion`
- `confidence`
- `smileScore`
- `browTension`
- `eyeOpenness`
- `notes`

Use this as a reference set for expressions captured by the webcam flow.
Include messy real-world conditions when expanding it further, such as low light, soft smiles, partial face framing, glasses glare, motion blur, and head tilt.

## `facial_landmark_profiles.csv`

Suggested columns:

- `profileId`
- `emotion`
- `mouthWidthRatio`
- `mouthHeightRatio`
- `browRaiseRatio`
- `eyeOpenRatio`
- `cheekLiftRatio`
- `jawDropRatio`

This dataset is intended for facial-landmark based emotion classification work.
Keep multiple profile variants per emotion so the trainer can learn more realistic webcam-like face geometry instead of only one clean pattern.

## How to scale this

- Keep `id` consistent across both datasets.
- Start by joining your movie metadata to your emotion model output offline.
- Replace the starter rows with thousands of titles once your data pipeline is ready.
- Retrain the local ML models after dataset updates with `dotnet run -- --train-models`.
- Training now prepares cache files in `Data/training-cache` so larger datasets can be normalized cleanly before model fitting.
- After each retrain, metrics are written to `Data/models/training-report.json`.
