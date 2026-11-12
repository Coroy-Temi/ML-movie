namespace ML_MOVIE.Models.Admin;

public sealed class HomepageUpdateItemViewModel
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string Category { get; init; }
    public bool IsPublished { get; init; }
    public DateTime CreatedUtc { get; init; }
}
