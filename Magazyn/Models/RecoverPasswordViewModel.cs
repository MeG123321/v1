using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class RecoverPasswordViewModel
{
    [Required(ErrorMessage = "Login jest wymagany")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adres e-mail jest wymagany")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail")]
    public string Email { get; set; } = string.Empty;
}