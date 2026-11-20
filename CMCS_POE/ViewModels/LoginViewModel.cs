using System.ComponentModel.DataAnnotations;

namespace CMCS_POE.ViewModels;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(128)]
    public string Password { get; set; } = string.Empty;
    [StringLength(2048)]
    public string? ReturnUrl { get; set; }
}
