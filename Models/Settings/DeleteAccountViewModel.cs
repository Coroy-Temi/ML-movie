using System.ComponentModel.DataAnnotations;

namespace ML_MOVIE.Models.Settings;

public sealed class DeleteAccountViewModel
{
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
