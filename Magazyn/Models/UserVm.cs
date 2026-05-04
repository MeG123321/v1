using System;
using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class UserVm
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Login jest wymagany")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "Login musi mieć od 5 do 20 znaków")]
    [RegularExpression(@"^(?=.*[a-zA-Z0-9])[a-zA-Z0-9_]+$", ErrorMessage = "Login może zawierać tylko litery, cyfry i podkreślnik")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Hasło jest wymagane")]
    [StringLength(15, MinimumLength = 8, ErrorMessage = "Hasło musi mieć od 8 do 15 znaków")]
    // jeśli w edycji hasło ma być opcjonalne (zmiana tylko gdy wpisane) — powiedz, przerobię
    [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[-,!*#$&]).{8,15}$",
        ErrorMessage = "Hasło 8–15 znaków: 1 wielka, 1 mała litera, 1 cyfra i 1 znak specjalny (-, !, *, #, $, &)")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Imię jest wymagane")]
    [StringLength(50, ErrorMessage = "Imię może mieć maksymalnie 50 znaków")]
    [RegularExpression(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ ]+$", ErrorMessage = "Imię może zawierać tylko litery")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Nazwisko jest wymagane")]
    [StringLength(50, ErrorMessage = "Nazwisko może mieć maksymalnie 50 znaków")]
    [RegularExpression(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ]+([ \-][a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ]+)?$", ErrorMessage = "Nazwisko może zawierać tylko litery i myślnik")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "PESEL jest wymagany")]
    [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "Numer PESEL musi składać się z 11 cyfr")]
    public string Pesel { get; set; } = "";

    [Required(ErrorMessage = "Data urodzenia jest wymagana")]
    [DataType(DataType.Date)]
    public DateOnly? DataUrodzenia { get; set; }

    [Required(ErrorMessage = "Płeć jest wymagana")]
    [StringLength(20)]
    public string Plec { get; set; } = "";

    [Required(ErrorMessage = "Email jest wymagany")]
    [StringLength(255, ErrorMessage = "E-mail może mieć maksymalnie 255 znaków")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Nieprawidłowy format adresu e-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Telefon jest wymagany")]
    [RegularExpression(@"^[0-9]{9}$", ErrorMessage = "Numer telefonu musi zawierać dokładnie 9 cyfr")]
    public string NrTelefonu { get; set; } = "";

    [Required(ErrorMessage = "Status jest wymagany")]
    [StringLength(30)]
    public string Status { get; set; } = "";

    // Rola zwykle jest z joinów, więc może być null/readonly w widoku
    [StringLength(50)]
    public string? Rola { get; set; }

    [Required(ErrorMessage = "Miejscowość jest wymagana")]
    [StringLength(100, ErrorMessage = "Miejscowość może mieć maksymalnie 100 znaków")]
    [RegularExpression(@"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ ]+$", ErrorMessage = "Miejscowość może zawierać tylko litery")]
    public string Miejscowosc { get; set; } = "";

    [Required(ErrorMessage = "Kod pocztowy jest wymagany")]
    [RegularExpression(@"^[0-9]{2}-[0-9]{3}$", ErrorMessage = "Kod pocztowy musi mieć format 00-000")]
    public string KodPocztowy { get; set; } = "";

    [StringLength(100, ErrorMessage = "Ulica może mieć maksymalnie 100 znaków")]
    public string? Ulica { get; set; }

    [Required(ErrorMessage = "Numer posesji jest wymagany")]
    [StringLength(10, ErrorMessage = "Numer posesji może mieć maksymalnie 10 znaków")]
    [RegularExpression(@"^[a-zA-Z0-9/ ]+$", ErrorMessage = "Nieprawidłowy format numeru posesji")]
    public string NrPosesji { get; set; } = "";

    [StringLength(10, ErrorMessage = "Numer lokalu może mieć maksymalnie 10 znaków")]
    public string? NrLokalu { get; set; }

    public int Zapomniany { get; set; } = 0;
}