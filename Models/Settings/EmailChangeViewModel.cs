using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Settings;

public sealed class EmailChangeViewModel
{
    [Required, EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}
