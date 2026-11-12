namespace ML_MOVIE.Entities;

public sealed class ContactMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Subject { get; set; }
    public required string Message { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
