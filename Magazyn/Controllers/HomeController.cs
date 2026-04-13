using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

/// <summary>
/// Kontroler główny obsługujący stronę startową, logowanie oraz podgląd użytkowników przez API.
/// </summary>
public class HomeController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IWebHostEnvironment env, ILogger<HomeController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>Pełna ścieżka do pliku bazy danych SQLite.</summary>
    private string DbPath => Db.GetDbPath(_env);

    /// <summary>
    /// Usuwa znaki nowej linii z wartości wejściowej, aby zapobiec fałszowaniu wpisów w logach.
    /// </summary>
    private static string SL(string? value) =>
        (value ?? "").Replace('\r', '_').Replace('\n', '_');

    /// <summary>
    /// Konwertuje wartość kolumny Status z bazy danych (INT lub DBNull)
    /// na czytelny ciąg tekstowy: "Aktywny" lub "Nieaktywny".
    /// </summary>
    /// <param name="dbValue">Wartość odczytana z kolumny Status (może być DBNull).</param>
    /// <returns>"Aktywny" gdy Status = 1, w przeciwnym razie "Nieaktywny".</returns>
    private static string StatusToText(object dbValue)
    {
        if (dbValue == DBNull.Value) return "Nieaktywny";
        return Convert.ToInt32(dbValue) == 1 ? "Aktywny" : "Nieaktywny";
    }

    public IActionResult Index() => View();

    // =========================
    // LOGOWANIE
    // =========================

    /// <summary>
    /// Weryfikuje dane logowania użytkownika w bazie danych.
    /// Sprawdza login i hasło (bez rozróżnienia wielkości liter dla loginu),
    /// pomijając konta oznaczone jako zapomniane (RODO).
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

        // Weryfikacja danych logowania i pobranie id użytkownika.
        // LOWER(TRIM(...)) zapewnia odporność na różnice w wielkości liter i zbędne spacje.
        // COALESCE(czy_zapomniany,0) = 0 wyklucza konta usunięte zgodnie z RODO.
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

        // Pobranie ról użytkownika jako osobnych wierszy dla prawidłowego działania [Authorize(Roles=...)].
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
        return RedirectToAction(nameof(Index));
    }

    // =========================
    // API: lista userów
    // =========================

    /// <summary>
    /// Zwraca listę aktywnych użytkowników (nie-zapomnianych) w formacie JSON.
    /// Umożliwia filtrowanie po loginie, nazwisku lub peselu (częściowe dopasowanie, case-insensitive).
    /// Używana przez JavaScript do dynamicznego podglądu użytkowników.
    /// </summary>
    /// <param name="login">Opcjonalny filtr na login (username).</param>
    /// <param name="name">Opcjonalny filtr na imię i nazwisko.</param>
    /// <param name="pesel">Opcjonalny filtr na PESEL.</param>
    [Authorize]
    [HttpGet]
    public IActionResult ApiUsers(string? login = null, string? name = null, string? pesel = null)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { error = "Brak pliku bazy", path = DbPath });

        var userList = new List<object>();

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Pobiera podstawowe dane użytkowników z opcjonalnym filtrowaniem.
        // Parametry NULL wyłączają dany filtr (brak ograniczenia).
        command.CommandText = @"
SELECT id, username, firstName, LastName, pesel, Email
FROM Uzytkownicy
WHERE COALESCE(czy_zapomniany,0) = 0
  AND ($login IS NULL OR LOWER(TRIM(username)) LIKE '%' || LOWER(TRIM($login)) || '%')
  AND ($name  IS NULL OR LOWER(TRIM(firstName || ' ' || LastName)) LIKE '%' || LOWER(TRIM($name)) || '%')
  AND ($pesel IS NULL OR TRIM(pesel) LIKE '%' || TRIM($pesel) || '%')
ORDER BY id;
";
        command.Parameters.AddWithValue("$login", (object?)login ?? DBNull.Value);
        command.Parameters.AddWithValue("$name",  (object?)name  ?? DBNull.Value);
        command.Parameters.AddWithValue("$pesel", (object?)pesel ?? DBNull.Value);

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            userList.Add(new
            {
                id        = dbReader["id"],
                username  = dbReader["username"],
                firstName = dbReader["firstName"],
                lastName  = dbReader["LastName"],
                email     = dbReader["Email"],
                pesel     = dbReader["pesel"]
            });
        }

        return Json(userList);
    }

    // =========================
    // API: jeden user
    // =========================

    /// <summary>
    /// Zwraca pełne dane jednego użytkownika (łącznie z hasłem i danymi adresowymi) w formacie JSON.
    /// Używana przez panel edycji do wstępnego załadowania formularza przez AJAX.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika w tabeli Uzytkownicy.</param>
    [Authorize]
    [HttpGet]
    public IActionResult ApiUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Pobieramy kompletny rekord użytkownika wraz z danymi adresowymi i polami RODO.
        command.CommandText = @"
SELECT id, username, Password, firstName, LastName, pesel, Status, Plec, DataUrodzenia,
       Email, NrTelefonu,
       Miejscowosc, KodPocztowy, numer_posesji, Ulica, NrLokalu,
       COALESCE(czy_zapomniany,0) AS czy_zapomniany,
       DataZapomnienia,
       ZapomnialUserId
FROM Uzytkownicy
WHERE id = $id
LIMIT 1;
";
        command.Parameters.AddWithValue("$id", id);

        using var dbReader = command.ExecuteReader();
        if (!dbReader.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        var statusInt = dbReader["Status"] == DBNull.Value ? 0 : Convert.ToInt32(dbReader["Status"]);

        return Json(new
        {
            id           = dbReader["id"],
            username     = dbReader["username"],
            password     = dbReader["Password"],
            firstName    = dbReader["firstName"],
            lastName     = dbReader["LastName"],
            pesel        = dbReader["pesel"],

            // Status: wartość liczbowa (0/1) oraz odpowiadający tekst
            statusInt    = statusInt,
            status       = StatusToText(dbReader["Status"]),

            plec         = dbReader["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(dbReader["Plec"]),
            dataUrodzenia= dbReader["DataUrodzenia"],
            email        = dbReader["Email"],
            nrTelefonu   = dbReader["NrTelefonu"],
            miejscowosc  = dbReader["Miejscowosc"],
            kodPocztowy  = dbReader["KodPocztowy"],
            nrPosesji    = dbReader["numer_posesji"],
            ulica        = dbReader["Ulica"],
            nrLokalu     = dbReader["NrLokalu"],
            zapomniany   = Convert.ToInt32(dbReader["czy_zapomniany"]) == 1,
            dataZapomnienia  = dbReader["DataZapomnienia"],
            zapomnialUserId  = dbReader["ZapomnialUserId"]
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}