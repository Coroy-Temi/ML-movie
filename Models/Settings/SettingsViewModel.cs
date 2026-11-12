namespace ML_MOVIE.Models.Settings;

public sealed class SettingsViewModel
{
    public required ProfileSettingsViewModel Profile { get; init; }
    public required PasswordSettingsViewModel Password { get; init; }
    public required EmailVerificationViewModel Verification { get; init; }
    public required EmailChangeViewModel EmailChange { get; init; }
    public required DeleteAccountViewModel DeleteAccount { get; init; }
    public required ContactUsViewModel ContactUs { get; init; }
    public required string VerificationDestination { get; init; }
    public required string? OutboxPreview { get; init; }
}
