using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Auth.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username or email address.")]
    [Display(Name = "Username or email address")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}