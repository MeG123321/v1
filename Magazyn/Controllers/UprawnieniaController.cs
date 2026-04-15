using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

/// <summary>
/// Kontroler zarządzający uprawnieniami (rolami) użytkowników.
/// Umożliwia podgląd użytkowników przypisanych do danych ról oraz nadawanie ról.
/// </summary>
[Authorize]
public class UprawnieniaController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UprawnieniaController> _logger;

    public UprawnieniaController(IWebHostEnvironment env, ILogger<UprawnieniaController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>Pełna ścieżka do pliku bazy danych SQLite.</summary>
    private string DbPath => Db.GetDbPath(_env);

    /// <summary>Usuwa znaki nowej linii z wartości wejściowej, aby zapobiec fałszowaniu wpisów w logach.</summary>
    private static string SL(string? value) =>
        (value ?? "").Replace('\r', '_').Replace('\n', '_');

    // ============================================
    // NADAJ UPRAWNIENIA - lista użytkowników z filtrem ról
    // ============================================

    /// <summary>
    /// Wyświetla listę wszystkich użytkowników z możliwością filtrowania po zaznaczonych rolach.
    /// Jeśli żadna rola nie jest zaznaczona, wyświetlani są wszyscy użytkownicy.
    /// </summary>
    /// <param name="rola">Tablica nazw ról wybranych jako filtr (z checkboxów).</param>
    [HttpGet]
    public IActionResult Uprawnienia(string[]? rola = null)
    {
        var selectedRoles = rola?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToArray() ?? Array.Empty<string>();

        ViewBag.SelectedRoles = selectedRoles;

        var userList = new List<UserListRowDto>();
        if (!System.IO.File.Exists(DbPath))
            return View(userList);

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        var sql = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.Email,
       u.pesel,
       CASE WHEN u.Status = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS Status,
       COALESCE(GROUP_CONCAT(p.Nazwa, ', '), '-') AS Rola
FROM Uzytkownicy u
LEFT JOIN Uzytkownik_Uprawnienia uu ON uu.uzytkownik_id = u.id
LEFT JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
WHERE COALESCE(u.czy_zapomniany,0) = 0";

        if (selectedRoles.Length > 0)
        {
            // Filtruj użytkowników posiadających co najmniej jedną z zaznaczonych ról
            var placeholders = string.Join(",", selectedRoles.Select((_, i) => $"$r{i}"));
            sql += $@"
  AND u.id IN (
    SELECT uu2.uzytkownik_id
    FROM Uzytkownik_Uprawnienia uu2
    JOIN Uprawnienia p2 ON p2.Id = uu2.uprawnienie_id
    WHERE TRIM(p2.Nazwa) IN ({placeholders})
  )";
            for (int i = 0; i < selectedRoles.Length; i++)
                command.Parameters.AddWithValue($"$r{i}", selectedRoles[i]);
        }

        sql += @"
GROUP BY u.id, u.username, u.firstName, u.LastName, u.Email, u.pesel
ORDER BY u.id;";

        command.CommandText = sql;

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            userList.Add(new UserListRowDto
            {
                Id        = Convert.ToInt64(dbReader["id"]),
                Username  = dbReader["username"]?.ToString(),
                FirstName = dbReader["firstName"]?.ToString(),
                LastName  = dbReader["LastName"]?.ToString(),
                Email     = dbReader["Email"]?.ToString(),
                Pesel     = dbReader["pesel"]?.ToString(),
                Status    = dbReader["Status"]?.ToString(),
                Rola      = dbReader["Rola"]?.ToString()
            });
        }

        return View(userList);
    }

    // ============================================
    // USERS BY ROLE (zachowane dla kompatybilności)
    // ============================================

    /// <summary>
    /// Zwraca listę aktywnych użytkowników przypisanych do wskazanej roli (uprawnienia).
    /// </summary>
    /// <param name="rola">Nazwa roli (wartość kolumny Nazwa z tabeli Uprawnienia).</param>
    [HttpGet]
    public IActionResult UsersByRole(string rola)
    {
        if (string.IsNullOrWhiteSpace(rola))
            return BadRequest(new { msg = "Brak parametru rola" });

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        rola = rola.Trim();

        using var connection = Db.OpenConnection(DbPath);

        long roleId;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($nazwaRoli) LIMIT 1;";
            command.Parameters.AddWithValue("$nazwaRoli", rola);
            var roleIdScalar = command.ExecuteScalar();
            if (roleIdScalar == null)
                return NotFound(new { msg = "Nie znaleziono roli w tabeli Uprawnienia", rola });
            roleId = Convert.ToInt64(roleIdScalar);
        }

        var userList = new List<UserListRowDto>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.Email,
       u.pesel,
       CASE WHEN u.Status = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS Status,
       $nazwaRoli AS Rola
FROM Uzytkownik_Uprawnienia uu
JOIN Uzytkownicy u ON u.id = uu.uzytkownik_id
WHERE uu.uprawnienie_id = $roleId
  AND COALESCE(u.czy_zapomniany,0) = 0
ORDER BY u.LastName, u.firstName, u.username;
";
            command.Parameters.AddWithValue("$roleId", roleId);
            command.Parameters.AddWithValue("$nazwaRoli", rola);

            using var dbReader = command.ExecuteReader();
            while (dbReader.Read())
            {
                userList.Add(new UserListRowDto
                {
                    Id        = Convert.ToInt64(dbReader["id"]),
                    Username  = dbReader["username"]?.ToString(),
                    FirstName = dbReader["firstName"]?.ToString(),
                    LastName  = dbReader["LastName"]?.ToString(),
                    Email     = dbReader["Email"]?.ToString(),
                    Pesel     = dbReader["pesel"]?.ToString(),
                    Status    = dbReader["Status"]?.ToString(),
                    Rola      = dbReader["Rola"]?.ToString()
                });
            }
        }

        ViewBag.Rola = rola;
        return View(userList);
    }

    // ============================================
    // NADAJ UPRAWNIENIA (POST)
    // ============================================

    /// <summary>
    /// Nadaje użytkownikowi wybrane role (uprawnienia).
    /// Usuwa wszystkie dotychczasowe role użytkownika i wstawia zaznaczone.
    /// Użytkownik może posiadać wiele ról jednocześnie.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika.</param>
    /// <param name="rola">Tablica nazw ról do nadania (z checkboxów).</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRole(long id, string[]? rola = null)
    {
        var selectedRoles = rola?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct()
            .ToArray() ?? Array.Empty<string>();

        _logger.LogInformation("[AdminAccess] '{User}' nadaje role [{Role}] użytkownikowi id={TargetId} IP={RemoteIp}",
            SL(User.Identity?.Name), string.Join(", ", selectedRoles.Select(SL)), id, HttpContext.Connection.RemoteIpAddress);

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);

        // Krok 1: usuń wszystkie poprzednie role użytkownika
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uzytkownikId;";
            deleteCommand.Parameters.AddWithValue("$uzytkownikId", id);
            deleteCommand.ExecuteNonQuery();
        }

        // Krok 2: wstaw każdą z wybranych ról
        foreach (var roleName in selectedRoles)
        {
            long permissionId;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($nazwaRoli) LIMIT 1;";
                command.Parameters.AddWithValue("$nazwaRoli", roleName);
                var scalar = command.ExecuteScalar();
                if (scalar == null)
                {
                    _logger.LogWarning("[AdminAccess] Nieznana rola '{Rola}' pominięta przy nadawaniu użytkownikowi id={TargetId}", SL(roleName), id);
                    continue;
                }
                permissionId = Convert.ToInt64(scalar);
            }

            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = @"
INSERT OR IGNORE INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id)
VALUES ($permissionId, $uzytkownikId);
";
                insertCommand.Parameters.AddWithValue("$permissionId", permissionId);
                insertCommand.Parameters.AddWithValue("$uzytkownikId", id);
                insertCommand.ExecuteNonQuery();
            }
        }

        return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
    }
}