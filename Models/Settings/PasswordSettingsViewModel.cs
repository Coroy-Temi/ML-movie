using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Settings;

public sealed class PasswordSettingsViewModel
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
