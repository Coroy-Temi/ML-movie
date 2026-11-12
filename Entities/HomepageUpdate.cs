namespace ML_MOVIE.Entities;

public sealed class HomepageUpdate
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Category { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
