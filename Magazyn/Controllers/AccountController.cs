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
/// Logowanie: Email + Password (Username jest tylko nazwą użytkownika).
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
    /// Weryfikuje dane logowania użytkownika w bazie danych (Email + Password).
    /// Po pomyślnej weryfikacji wystawia cookie uwierzytelniające.
    /// Na razie pomijamy role/uprawnienia.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Brak bazy danych: {DbPath}");
            return View(model);
        }

        var email = (model.Email ?? "").Trim();
        var password = (model.Password ?? "").Trim();

        using var connection = Db.OpenConnection(DbPath);

        long userId;
        string username;

        using (var authCommand = connection.CreateCommand())
        {
            authCommand.CommandText = @"
SELECT id, username
FROM Uzytkownicy
WHERE LOWER(TRIM(Email)) = LOWER(TRIM($email))
  AND TRIM(COALESCE(Password,'')) = TRIM($password)
  AND COALESCE(czy_zapomniany,0) = 0
LIMIT 1;
";
            authCommand.Parameters.AddWithValue("$email", email);
            authCommand.Parameters.AddWithValue("$password", password);

            using var r = authCommand.ExecuteReader();
            if (!r.Read())
            {
                _logger.LogWarning("[Auth] Nieudane logowanie email='{Email}' IP={RemoteIp}",
                    SL(email), HttpContext.Connection.RemoteIpAddress);

                ModelState.AddModelError("", "Błędny e-mail lub hasło");
                return View(model);
            }

            userId = Convert.ToInt64(r["id"]);
            username = r["username"]?.ToString() ?? email;
        }

        // Bez uprawnień/rol na razie:
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email)
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

        _logger.LogInformation("[Auth] Zalogowano user='{Username}' id={UserId} email='{Email}' IP={RemoteIp}",
            SL(username), userId, SL(email), HttpContext.Connection.RemoteIpAddress);

        return RedirectToAction("AdminPanel", "Uzytkownicy");
    }

    // =========================
    // WYLOGOWANIE
    // =========================

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("[Auth] Użytkownik '{Username}' wylogował się z IP {RemoteIp}",
            User.Identity?.Name ?? "nieznany", HttpContext.Connection.RemoteIpAddress);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}