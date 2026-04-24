using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Magazyn.Controllers
{
    [Authorize(Roles = "Administrator,Kierownik sprzedazy,Kierownik magazynu")]
    public class UprawnieniaController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public UprawnieniaController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string DbPath => Db.GetDbPath(_env);

        // =========================================
        // SIŁA RÓL (hierarchia uprawnień)
        // =========================================
        private static readonly Dictionary<string, int> RolePower = new()
        {
            { "Administrator", 10 },
            { "Kierownik sprzedazy", 5 },
            { "Kierownik magazynu", 5 },
            { "Sprzedawca", 1 },
            { "Magazynier", 1 }
        };

        private int GetMaxRolePower(IEnumerable<string> roles)
        {
            return roles
                .Where(r => RolePower.ContainsKey(r))
                .Select(r => RolePower[r])
                .DefaultIfEmpty(0)
                .Max();
        }

        // =========================================
        // LISTA UŻYTKOWNIKÓW + FILTROWANIE
        // =========================================
        [HttpGet]
        public IActionResult Uprawnienia(string[]? rola = null)
        {
            var selectedRoles = rola?
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            ViewBag.SelectedRoles = selectedRoles;

            var userList = new List<UserListRowDto>();

            using var connection = Db.OpenConnection(DbPath);
            using var command = connection.CreateCommand();

            var sql = @"
                SELECT u.id,
                       u.username,
                       u.firstName,
                       u.LastName,
                       u.Email,
                       COALESCE(GROUP_CONCAT(p.Nazwa, ', '), '-') AS Rola
                FROM Uzytkownicy u
                LEFT JOIN Uzytkownik_Uprawnienia uu ON uu.uzytkownik_id = u.id
                LEFT JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
                WHERE COALESCE(u.czy_zapomniany, 0) = 0";

            // 🔥 FILTR RÓL (przywrócony)
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
                GROUP BY u.id, u.username, u.firstName, u.LastName, u.Email
                ORDER BY u.id";

            command.CommandText = sql;

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                userList.Add(new UserListRowDto
                {
                    Id = Convert.ToInt64(reader["id"]),
                    Username = reader["username"]?.ToString(),
                    FirstName = reader["firstName"]?.ToString(),
                    LastName = reader["LastName"]?.ToString(),
                    Email = reader["Email"]?.ToString(),
                    Rola = reader["Rola"]?.ToString()
                });
            }

            return View(userList);
        }

        // =========================================
        // ZMIANA RÓL (z kontrolą hierarchii)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetRole(long id, string[]? rola = null)
        {
            var newRoles = rola?
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct()
                .ToList() ?? new List<string>();

            using var connection = Db.OpenConnection(DbPath);

            // stare role użytkownika
            var oldRoles = new List<string>();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT p.Nazwa
                    FROM Uprawnienia p
                    JOIN Uzytkownik_Uprawnienia uu ON p.Id = uu.uprawnienie_id
                    WHERE uu.uzytkownik_id = $id";

                cmd.Parameters.AddWithValue("$id", id);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                    oldRoles.Add(r.GetString(0).Trim());
            }

            // role aktualnego usera
            var currentUserRoles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            int currentPower = GetMaxRolePower(currentUserRoles);
            int targetPower = GetMaxRolePower(oldRoles);
            int newPower = GetMaxRolePower(newRoles);

            // nie możesz zmieniać wyższych
            if (targetPower >= currentPower)
            {
                TempData["Message"] = "Nie możesz modyfikować użytkownika o równych lub wyższych uprawnieniach.";
                return RedirectToAction("Uprawnienia");
            }

            // nie możesz nadać zbyt wysokich ról
            if (newPower >= currentPower)
            {
                TempData["Message"] = "Nie możesz nadać roli równej lub wyższej niż Twoja.";
                return RedirectToAction("Uprawnienia");
            }

            // usuń stare role
            using (var del = connection.CreateCommand())
            {
                del.CommandText = "DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $id";
                del.Parameters.AddWithValue("$id", id);
                del.ExecuteNonQuery();
            }

            // dodaj nowe role
            foreach (var role in newRoles)
            {
                using var ins = connection.CreateCommand();
                ins.CommandText = @"
                    INSERT INTO Uzytkownik_Uprawnienia (uzytkownik_id, uprawnienie_id)
                    SELECT $uid, Id FROM Uprawnienia WHERE Nazwa = $n";

                ins.Parameters.AddWithValue("$uid", id);
                ins.Parameters.AddWithValue("$n", role);
                ins.ExecuteNonQuery();
            }

            TempData["Message"] = newRoles.Count > 0
                ? "Nadano role: " + string.Join(", ", newRoles)
                : "Odebrano wszystkie uprawnienia.";

            return RedirectToAction("Uprawnienia");
        }
    }
}