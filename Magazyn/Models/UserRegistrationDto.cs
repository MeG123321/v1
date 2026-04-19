using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc; // Wymagane dla [Remote]

namespace Magazyn.Models;

public class UserRegistrationDto
{
    [Required(ErrorMessage = "Nazwa użytkownika jest wymagana")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "Nazwa użytkownika musi mieć od 5 do 20 znaków")]
    [RegularExpression(@"^(?=.*[a-zA-Z0-9])[a-zA-Z0-9_]+$", ErrorMessage = "Nazwa użytkownika może zawierać tylko litery, cyfry i podkreślnik")]
    [Remote(action: "CheckUsername", controller: "Uzytkownicy")] // Sprawdzanie w tle
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Hasło jest wymagane")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
        ErrorMessage = "Hasło musi zawierać: małą literę, dużą literę, cyfrę oraz znak specjalny")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Imię jest wymagane")]
    [StringLength(50, ErrorMessage = "Imię może mieć maksymalnie 50 znaków")]
    [RegularExpression(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ ]+$", ErrorMessage = "Imię może zawierać tylko litery")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Nazwisko jest wymagane")]
    [StringLength(50, ErrorMessage = "Nazwisko może mieć maksymalnie 50 znaków")]
    [RegularExpression(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ]+([ \-][a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ]+)?$",
        ErrorMessage = "Nazwisko może zawierać tylko litery oraz myślnik")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Adres e-mail jest wymagany")]
    [StringLength(255, ErrorMessage = "E-mail może mieć maksymalnie 255 znaków")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail")]
    [Remote(action: "CheckEmail", controller: "Uzytkownicy")] // Sprawdzanie w tle
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "PESEL jest wymagany")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "PESEL musi składać się z 11 cyfr")]
    [Remote(action: "CheckPesel", controller: "Uzytkownicy")] // Sprawdzanie w tle
    public string Pesel { get; set; } = "";

    [Required(ErrorMessage = "Numer telefonu jest wymagany")]
    [RegularExpression(@"^\d{9}$", ErrorMessage = "Numer telefonu musi składać się z 9 cyfr")]
    public string NrTelefonu { get; set; } = "";

    [Required(ErrorMessage = "Płeć jest wymagana")]
    [StringLength(20)]
    public string Plec { get; set; } = "";

    [Required(ErrorMessage = "Status jest wymagany")]
    [StringLength(30)]
    public string Status { get; set; } = "";

    public string? Rola { get; set; }

    [Required(ErrorMessage = "Data urodzenia jest wymagana")]
    [DataType(DataType.Date)]
    public DateOnly? DataUrodzenia { get; set; }

    [Required(ErrorMessage = "Miejscowość jest wymagana")]
    [StringLength(100)]
    [RegularExpression(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ ]+$", ErrorMessage = "Miejscowość może zawierać tylko litery")]
    public string Miejscowosc { get; set; } = "";

    [Required(ErrorMessage = "Kod pocztowy jest wymagany")]
    [RegularExpression(@"^\d{2}-\d{3}$", ErrorMessage = "Kod pocztowy w formacie 00-000")]
    public string KodPocztowy { get; set; } = "";

    [Required(ErrorMessage = "Numer posesji jest wymagany")]
    [StringLength(10, ErrorMessage = "Numer posesji może mieć maksymalnie 10 znaków")]
    [RegularExpression(@"^[a-zA-Z0-9/ ]+$", ErrorMessage = "Nieprawidłowy format numeru posesji")]
    public string NrPosesji { get; set; } = "";

    [StringLength(100)]
    public string? Ulica { get; set; }

    [StringLength(10)]
    public string? NrLokalu { get; set; }
}