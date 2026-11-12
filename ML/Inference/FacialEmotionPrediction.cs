using Microsoft.ML.Data;

namespace ML_MOVIE.ML.Inference;

public sealed class FacialEmotionPrediction
{
    [ColumnName("PredictedLabel")]
    public string PredictedLabel { get; set; } = string.Empty;

    public float[] Score { get; set; } = [];
}
