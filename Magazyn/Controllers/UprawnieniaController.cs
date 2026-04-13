using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

/// <summary>
/// Kontroler zarządzający uprawnieniami (rolami) użytkowników.
/// Umożliwia podgląd użytkowników przypisanych do danej roli oraz nadawanie ról.
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
    // UPRAWNIENIA - lista ról
    // ============================================
    [HttpGet]
    public IActionResult Uprawnienia() => View();

    // ============================================
    // USERS BY ROLE
    // ============================================

    /// <summary>
    /// Zwraca listę aktywnych użytkowników przypisanych do wskazanej roli (uprawnienia).
    /// Najpierw wyszukuje identyfikator roli w tabeli Uprawnienia, a następnie
    /// pobiera powiązanych użytkowników przez tabelę pośrednią Uzytkownik_Uprawnienia.
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

        // Krok 1: pobierz identyfikator roli z tabeli Uprawnienia
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

        // Krok 2: pobierz wszystkich nieaktywnych (niezapomnianych) użytkowników z daną rolą
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
    /// Nadaje użytkownikowi wskazaną rolę (uprawnienie).
    /// Najpierw usuwa wszystkie dotychczasowe role użytkownika,
    /// a następnie wstawia nową – każdy użytkownik może mieć tylko jedną rolę.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika.</param>
    /// <param name="rola">Nazwa roli do nadania.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRole(long id, string rola)
    {
        _logger.LogInformation("[AdminAccess] '{User}' nadaje rolę '{Rola}' użytkownikowi id={TargetId} IP={RemoteIp}",
            SL(User.Identity?.Name), SL(rola), id, HttpContext.Connection.RemoteIpAddress);

        if (string.IsNullOrWhiteSpace(rola))
            return BadRequest(new { msg = "Brak roli" });

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);

        // Krok 1: pobierz identyfikator wybranej roli
        long permissionId;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($nazwaRoli) LIMIT 1;";
            command.Parameters.AddWithValue("$nazwaRoli", rola.Trim());
            var permissionIdScalar = command.ExecuteScalar();
            if (permissionIdScalar == null)
                return BadRequest(new { msg = "Nie ma takiego uprawnienia w tabeli Uprawnienia" });
            permissionId = Convert.ToInt64(permissionIdScalar);
        }

        // Krok 2: usuń wszystkie poprzednie role użytkownika
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uzytkownikId;";
            deleteCommand.Parameters.AddWithValue("$uzytkownikId", id);
            deleteCommand.ExecuteNonQuery();
        }

        // Krok 3: wstaw nową rolę
        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = @"
INSERT INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id)
VALUES ($permissionId, $uzytkownikId);
";
            insertCommand.Parameters.AddWithValue("$permissionId", permissionId);
            insertCommand.Parameters.AddWithValue("$uzytkownikId", id);
            insertCommand.ExecuteNonQuery();
        }

        return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
    }
}