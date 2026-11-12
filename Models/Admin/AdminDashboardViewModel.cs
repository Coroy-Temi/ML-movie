namespace ML_MOVIE.Models.Admin;

public sealed class AdminDashboardViewModel
{
    public required IReadOnlyList<HomepageUpdateItemViewModel> Updates { get; init; }
    public required HomepageUpdateEditViewModel NewUpdate { get; init; }
    public required GrantAdminViewModel GrantAdmin { get; init; }
    public required int UserCount { get; init; }
    public required int VerifiedUserCount { get; init; }
    public required int SessionCount { get; init; }
    public required int ContactMessageCount { get; init; }
}
