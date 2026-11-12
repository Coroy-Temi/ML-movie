using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Settings;

public sealed class ProfileSettingsViewModel
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public ThemeOption Theme { get; set; } = ThemeOption.Dark;
}
