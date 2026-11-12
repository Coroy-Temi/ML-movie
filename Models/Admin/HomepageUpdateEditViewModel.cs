using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Admin;

public sealed class HomepageUpdateEditViewModel
{
    [Required, StringLength(120, MinimumLength = 4)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(400, MinimumLength = 10)]
    public string Body { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Category { get; set; } = "News";

    public bool IsPublished { get; set; } = true;
}
