using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Admin;

public sealed class GrantAdminViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
