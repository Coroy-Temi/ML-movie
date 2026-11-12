using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models;

public sealed class FacialEmotionInput
{
    [Required]
    public string DetectedEmotion { get; set; } = "Happy";

    public string EmotionSource { get; set; } = "Camera prototype";

    public string GenreFilters { get; set; } = string.Empty;

    public string CaptureDataUrl { get; set; } = string.Empty;
}
