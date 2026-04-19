using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Nowe hasło jest wymagane")]
    [MinLength(6, ErrorMessage = "Hasło musi mieć co najmniej 6 znaków")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Powtórzenie hasła jest wymagane")]
    [Compare("NewPassword", ErrorMessage = "Hasła nie są zgodne")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}