using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;

namespace Magazyn.Controllers;

/// <summary>
/// Kontroler obsługujący logowanie i wylogowanie użytkownika.
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
    // LOGOWANIE
    // =========================

    /// <summary>
    /// Weryfikuje dane logowania użytkownika w bazie danych.
    /// Po pomyślnej weryfikacji wystawia cookie uwierzytelniające.
    /// </summary>
    /// <param name="username">Login użytkownika.</param>
    /// <param name="password">Hasło użytkownika.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return BadRequest(new { ok = false, msg = "Brak loginu lub hasła" });

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { ok = false, msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);

        long? userId;
        string? loggedUsername;
        using (var authCommand = connection.CreateCommand())
        {
            authCommand.CommandText = @"
SELECT id, username
FROM Uzytkownicy
WHERE LOWER(TRIM(username)) = LOWER(TRIM($username))
  AND TRIM(COALESCE(Password,'')) = TRIM($password)
  AND COALESCE(czy_zapomniany,0) = 0
LIMIT 1;
";
            authCommand.Parameters.AddWithValue("$username", username.Trim());
            authCommand.Parameters.AddWithValue("$password", password.Trim());
            using var authReader = authCommand.ExecuteReader();
            if (!authReader.Read())
            {
                _logger.LogWarning("[AdminAccess] Nieudana próba logowania dla użytkownika '{Username}' z IP {RemoteIp}",
                    SL(username), HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { ok = false, msg = "Błędne dane" });
            }
            userId        = Convert.ToInt64(authReader["id"]);
            loggedUsername = authReader["username"]?.ToString() ?? username;
        }

        var roles = new List<string>();
        using (var roleCommand = connection.CreateCommand())
        {
            roleCommand.CommandText = @"
SELECT p.Nazwa
FROM Uzytkownik_Uprawnienia uu
JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
WHERE uu.uzytkownik_id = $userId;
";
            roleCommand.Parameters.AddWithValue("$userId", userId);
            using var roleReader = roleCommand.ExecuteReader();
            while (roleReader.Read())
            {
                var roleName = roleReader["Nazwa"]?.ToString();
                if (!string.IsNullOrWhiteSpace(roleName))
                    roles.Add(roleName);
            }
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, loggedUsername),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()!)
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        var roleDisplay = string.Join(", ", roles);
        _logger.LogInformation("[AdminAccess] Użytkownik '{Username}' (id={UserId}, role={Roles}) zalogował się z IP {RemoteIp}",
            SL(loggedUsername), userId, SL(roleDisplay), HttpContext.Connection.RemoteIpAddress);

        return Json(new { ok = true, username = loggedUsername, role = roleDisplay });
    }

    // =========================
    // WYLOGOWANIE
    // =========================

    /// <summary>
    /// Wylogowuje zalogowanego użytkownika przez usunięcie cookie uwierzytelniającego.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("[AdminAccess] Użytkownik '{Username}' wylogował się z IP {RemoteIp}",
            User.Identity?.Name ?? "nieznany", HttpContext.Connection.RemoteIpAddress);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
