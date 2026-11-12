namespace ML_MOVIE.Models;

public sealed class FacialEmotionApiRequest
{
    public float SmileScore { get; set; }
    public float BrowTension { get; set; }
    public float EyeOpenness { get; set; }
    public float MouthWidthRatio { get; set; }
    public float MouthHeightRatio { get; set; }
    public float BrowRaiseRatio { get; set; }
    public float EyeOpenRatio { get; set; }
    public float CheekLiftRatio { get; set; }
    public float JawDropRatio { get; set; }
}
