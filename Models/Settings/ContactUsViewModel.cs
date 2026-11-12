using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Settings;

public sealed class ContactUsViewModel
{
    [Required, StringLength(140, MinimumLength = 4)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(2400, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;
}
