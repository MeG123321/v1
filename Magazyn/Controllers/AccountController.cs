using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

/// <summary>
/// Kontroler obsługujący logowanie i wylogowanie użytkownika.
/// Logowanie: Login + Password zgodnie z LG_UC1.
/// </summary>
public class AccountController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IWebHostEnvironment env, ILogger<AccountController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>Pełna ścieżka do pliku bazy danych SQLite.</summary>
    private string DbPath => Db.GetDbPath(_env);

    /// <summary>Usuwa znaki nowej linii z wartości wejściowej, aby zapobiec fałszowaniu wpisów w logach.</summary>
    private static string SL(string? value) =>
        (value ?? "").Replace('\r', '_').Replace('\n', '_');

    // =========================
    // LOGOWANIE (STRONA)
    // =========================

    [HttpGet]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    /// <summary>
    /// Weryfikuje dane logowania użytkownika w bazie danych (Login + Password).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Sprawdzenie czy wszystkie wymagane pola zostały uzupełnione (LG_UC1 Przebieg główny pkt 6)
        if (!ModelState.IsValid)
            return View(model);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Brak bazy danych: {DbPath}");
            return View(model);
        }

        var login = (model.Login ?? "").Trim();
        var password = (model.Password ?? "").Trim();

        using var connection = Db.OpenConnection(DbPath);

        long userId;
        string username;
        string userEmail;

        using (var authCommand = connection.CreateCommand())
        {
            // Weryfikacja istnienia użytkownika i poprawności hasła (LG_UC1 pkt 7 i 8)
            // Używamy kolumny 'username' jako loginu wejściowego
            authCommand.CommandText = @"
SELECT id, username, Email
FROM Uzytkownicy
WHERE LOWER(TRIM(username)) = LOWER(TRIM($login))
  AND TRIM(COALESCE(Password,'')) = TRIM($password)
  AND COALESCE(czy_zapomniany,0) = 0
LIMIT 1;
";
            authCommand.Parameters.AddWithValue("$login", login);
            authCommand.Parameters.AddWithValue("$password", password);

            using var r = authCommand.ExecuteReader();
            if (!r.Read())
            {
                // Scenariusz alternatywny B: Niepoprawne dane logowania
                _logger.LogWarning("[Auth] Nieudane logowanie login='{Login}' IP={RemoteIp}",
                    SL(login), HttpContext.Connection.RemoteIpAddress);

                ModelState.AddModelError("", "Niepoprawny login lub hasło");
                return View(model);
            }

            userId = Convert.ToInt64(r["id"]);
            username = r["username"]?.ToString() ?? login;
            userEmail = r["Email"]?.ToString() ?? "";
        }

        // Tworzenie sesji użytkownika (LG_UC1 pkt 11)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, userEmail)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var props = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe
        };
        
        if (model.RememberMe)
            props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

        _logger.LogInformation("[Auth] Zalogowano user='{Username}' id={UserId} IP={RemoteIp}",
            SL(username), userId, HttpContext.Connection.RemoteIpAddress);

        // System wyświetla główny widok aplikacji (LG_UC1 pkt 14)
        return RedirectToAction("AdminPanel", "Uzytkownicy");
    }

    // =========================
    // WYLOGOWANIE (LG_UC2)
    // =========================

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("[Auth] Użytkownik '{Username}' wylogował się z IP {RemoteIp}",
            User.Identity?.Name ?? "nieznany", HttpContext.Connection.RemoteIpAddress);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // Powrót do ekranu startowego/logowania (LG_UC2 pkt 5)
        return RedirectToAction("Index", "Home");
    }
}