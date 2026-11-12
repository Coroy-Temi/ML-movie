using Microsoft.ML.Data;

namespace ML_MOVIE.ML.Training;

public sealed class FacialEmotionTrainingRow
{
    [LoadColumn(0)]
    public float SmileScore { get; set; }
    [LoadColumn(1)]
    public float BrowTension { get; set; }
    [LoadColumn(2)]
    public float EyeOpenness { get; set; }
    [LoadColumn(3)]
    public float MouthWidthRatio { get; set; }
    [LoadColumn(4)]
    public float MouthHeightRatio { get; set; }
    [LoadColumn(5)]
    public float BrowRaiseRatio { get; set; }
    [LoadColumn(6)]
    public float EyeOpenRatio { get; set; }
    [LoadColumn(7)]
    public float CheekLiftRatio { get; set; }
    [LoadColumn(8)]
    public float JawDropRatio { get; set; }
    [LoadColumn(9)]
    public string Label { get; set; } = string.Empty;
}
