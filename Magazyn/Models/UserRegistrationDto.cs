using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class UserRegistrationDto
{
    [Required, StringLength(20, MinimumLength = 5)]
    public string Username { get; set; } = "";

    [Required, StringLength(15, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Required]
    public string FirstName { get; set; } = "";

    [Required]
    public string LastName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^\d{11}$")]
    public string Pesel { get; set; } = "";

    [Required, RegularExpression(@"^\d{9}$")]
    public string NrTelefonu { get; set; } = "";

    [Required]
    public string Plec { get; set; } = "";

    [Required]
    public string Status { get; set; } = "";

    [Required]
    public string Rola { get; set; } = "";

    [Required]
    public string DataUrodzenia { get; set; } = ""; // YYYY-MM-DD

    // ADRES
    [Required]
    public string Miejscowosc { get; set; } = "";

    [Required, RegularExpression(@"^\d{2}-\d{3}$", ErrorMessage = "Kod pocztowy w formacie 00-000")]
    public string KodPocztowy { get; set; } = "";

    [Required]
    public string NrPosesji { get; set; } = "";

    public string? Ulica { get; set; }
    public string? NrLokalu { get; set; }
}