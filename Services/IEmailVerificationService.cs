using ML_MOVIE.Entities;

namespace ML_MOVIE.Services;

public interface IEmailVerificationService
{
    Task<string> SendVerificationCodeAsync(AppUser user, string emailAddress);
    bool VerifyCode(AppUser user, string code);
}
