using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using System.Data;

namespace Magazyn.Controllers;

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

    private string DbPath => Db.GetDbPath(_env);

    private static string SL(string? value) =>
        (value ?? "").Replace('\r', '_').Replace('\n', '_');

    [HttpGet]
    public IActionResult Uprawnienia(string[]? rola = null)
    {
        // Czyszczenie i przygotowanie wybranych ról
        var selectedRoles = rola?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        ViewBag.SelectedRoles = selectedRoles;

        var userList = new List<UserListRowDto>();
        if (!System.IO.File.Exists(DbPath))
            return View(userList);

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Podstawowe zapytanie - pobiera wszystkich użytkowników i łączy ich role w jeden ciąg
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
            WHERE COALESCE(u.czy_zapomniany, 0) = 0";

        // Logika filtrowania AND: dla kazdej wybranej roli musi istniec przypisanie.
        if (selectedRoles.Length > 0)
        {
            for (int i = 0; i < selectedRoles.Length; i++)
            {
                sql += $@"
            AND EXISTS (
                SELECT 1
                FROM Uzytkownik_Uprawnienia uu2
                JOIN Uprawnienia p2 ON p2.Id = uu2.uprawnienie_id
                WHERE uu2.uzytkownik_id = u.id
                  AND TRIM(p2.Nazwa) = TRIM($r{i})
            )";

                command.Parameters.AddWithValue($"$r{i}", selectedRoles[i]);
            }
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRole(long id, string[]? rola = null)
    {
        var selectedRoles = rola?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct()
            .ToArray() ?? Array.Empty<string>();

        using var connection = Db.OpenConnection(DbPath);

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uzytkownikId;";
            deleteCommand.Parameters.AddWithValue("$uzytkownikId", id);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var roleName in selectedRoles)
        {
            long permissionId;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($nazwaRoli) LIMIT 1;";
                command.Parameters.AddWithValue("$nazwaRoli", roleName);
                var scalar = command.ExecuteScalar();
                if (scalar == null) continue;
                permissionId = Convert.ToInt64(scalar);
            }

            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = @"INSERT OR IGNORE INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id) VALUES ($permissionId, $uzytkownikId);";
                insertCommand.Parameters.AddWithValue("$permissionId", permissionId);
                insertCommand.Parameters.AddWithValue("$uzytkownikId", id);
                insertCommand.ExecuteNonQuery();
            }
        }

        return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
    }
}