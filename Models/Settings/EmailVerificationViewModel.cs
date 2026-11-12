using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Settings;

public sealed class EmailVerificationViewModel
{
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;

    [EmailAddress]
    public string PendingEmail { get; set; } = string.Empty;
}
