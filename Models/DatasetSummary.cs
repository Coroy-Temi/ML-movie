namespace ML_MOVIE.Models;

public sealed class DatasetSummary
{
    public required string Name { get; init; }
    public required string FileName { get; init; }
    public required string Description { get; init; }
    public required bool IsLoaded { get; init; }
    public required int RowCount { get; init; }
}
