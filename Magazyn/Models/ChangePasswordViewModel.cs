using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Hasło jest wymagane")]
    // Kopiujemy StringLength z Twojego UserRegistrationDto
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków")]
    // Kopiujemy dokładnie ten sam Regex, który masz w rejestracji
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
        ErrorMessage = "Hasło musi zawierać: małą literę, dużą literę, cyfrę oraz znak specjalny")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Powtórzenie hasła jest wymagane")]
    [Compare("NewPassword", ErrorMessage = "Hasła nie są zgodne")]
    public string ConfirmPassword { get; set; } = "";
}