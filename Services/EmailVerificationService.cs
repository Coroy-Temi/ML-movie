using System.Security.Cryptography;
using ML_MOVIE.Entities;

namespace ML_MOVIE.Services;

public sealed class EmailVerificationService : IEmailVerificationService
{
    private readonly IWebHostEnvironment _environment;

    public EmailVerificationService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SendVerificationCodeAsync(AppUser user, string emailAddress)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        user.VerificationCode = code;
        user.VerificationCodeExpiresUtc = DateTime.UtcNow.AddMinutes(10);

        var outboxDirectory = Path.Combine(_environment.ContentRootPath, "Data", "outbox");
        Directory.CreateDirectory(outboxDirectory);

        var body = $"""
        To: {emailAddress}
        Subject: MoviePulse verification code

        Your MoviePulse verification code is: {code}
        This code expires in 10 minutes.
        """;

        await File.WriteAllTextAsync(Path.Combine(outboxDirectory, $"verification-{user.Id}.txt"), body);
        return body;
    }

    public bool VerifyCode(AppUser user, string code)
    {
        if (string.IsNullOrWhiteSpace(user.VerificationCode) || user.VerificationCodeExpiresUtc is null)
        {
            return false;
        }

        var valid = user.VerificationCodeExpiresUtc > DateTime.UtcNow &&
                    string.Equals(user.VerificationCode, code?.Trim(), StringComparison.Ordinal);

        if (valid)
        {
            user.VerificationCode = string.Empty;
            user.VerificationCodeExpiresUtc = null;
        }

        return valid;
    }
}
