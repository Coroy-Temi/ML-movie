namespace ML_MOVIE.Models.Auth;

public sealed class AuthViewModel
{
    public required LoginViewModel Login { get; set; }
    public required RegisterViewModel Register { get; set; }
}
