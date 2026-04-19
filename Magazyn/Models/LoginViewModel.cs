using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Login jest wymagany")]
    [Display(Name = "Login")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hasło jest wymagane")]
    [DataType(DataType.Password)]
    [Display(Name = "Hasło")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}