using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using System.Data;

namespace Magazyn.Controllers;

[Authorize(Roles = "Administrator,Kierownik sprzedazy,Kierownik magazynu")]
public class UprawnieniaController : Controller
{
    private readonly IWebHostEnvironment _env;
    public UprawnieniaController(IWebHostEnvironment env) => _env = env;
    private string DbPath => Db.GetDbPath(_env);

    [HttpGet]
    public IActionResult Uprawnienia()
    {
        var userList = new List<UserListRowDto>();
        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.id, u.username, u.firstName, u.LastName, u.Email,
                   COALESCE(GROUP_CONCAT(p.Nazwa, ', '), '-') AS Rola
            FROM Uzytkownicy u
            LEFT JOIN Uzytkownik_Uprawnienia uu ON uu.uzytkownik_id = u.id
            LEFT JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
            WHERE COALESCE(u.czy_zapomniany, 0) = 0
            GROUP BY u.id ORDER BY u.id";

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read()) {
            userList.Add(new UserListRowDto {
                Id = Convert.ToInt64(dbReader["id"]),
                Username = dbReader["username"]?.ToString(),
                FirstName = dbReader["firstName"]?.ToString(),
                LastName = dbReader["LastName"]?.ToString(),
                Rola = dbReader["Rola"]?.ToString()
            });
        }
        return View(userList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRole(long id, string[]? rola = null)
    {
        // 1. Przygotuj listę nowych ról z formularza
        var newRoles = rola?.Where(r => !string.IsNullOrWhiteSpace(r))
                           .Select(r => r.Trim())
                           .Distinct()
                           .ToList() ?? new List<string>();

        // Blokada dla osób nie będących adminami
        if (!User.IsInRole("Administrator")) {
            newRoles.RemoveAll(r => r.Equals("Administrator", StringComparison.OrdinalIgnoreCase));
        }

        using var connection = Db.OpenConnection(DbPath);

        // 2. WAŻNE: Najpierw pobierz obecne role z bazy, ZANIM cokolwiek usuniesz
        var oldRoles = new List<string>();
        using (var cmd = connection.CreateCommand()) {
            cmd.CommandText = "SELECT p.Nazwa FROM Uprawnienia p JOIN Uzytkownik_Uprawnienia uu ON p.Id = uu.uprawnienie_id WHERE uu.uzytkownik_id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            while (r.Read()) oldRoles.Add(r.GetString(0).Trim());
        }

        // 3. Porównaj zestawy ról
        bool isIdentical = newRoles.Count == oldRoles.Count && 
                          !newRoles.Except(oldRoles, StringComparer.OrdinalIgnoreCase).Any() &&
                          !oldRoles.Except(newRoles, StringComparer.OrdinalIgnoreCase).Any();

        if (isIdentical && newRoles.Count > 0)
        {
            TempData["Message"] = "Użytkownik już posiada wybrane role: " + string.Join(", ", oldRoles);
            return RedirectToAction("Uprawnienia");
        }

        // 4. Jeśli role są inne, wykonaj aktualizację
        using (var del = connection.CreateCommand()) {
            del.CommandText = "DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $id";
            del.Parameters.AddWithValue("$id", id);
            del.ExecuteNonQuery();
        }

        foreach (var rName in newRoles) {
            using var cmdIns = connection.CreateCommand();
            cmdIns.CommandText = "INSERT INTO Uzytkownik_Uprawnienia (uzytkownik_id, uprawnienie_id) SELECT $uid, Id FROM Uprawnienia WHERE Nazwa = $n";
            cmdIns.Parameters.AddWithValue("$uid", id);
            cmdIns.Parameters.AddWithValue("$n", rName);
            cmdIns.ExecuteNonQuery();
        }

        TempData["Message"] = newRoles.Count > 0 ? "Nadano role: " + string.Join(", ", newRoles) : "Odebrano wszystkie uprawnienia.";
        return RedirectToAction("Uprawnienia");
    }
}