using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

public class UprawnieniaController : Controller
{
    private readonly IWebHostEnvironment _env;

    public UprawnieniaController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string DbPath => Db.GetDbPath(_env);

    // ============================================
    // UPRAWNIENIA - lista ról
    // ============================================
    [HttpGet]
    public IActionResult Uprawnienia() => View();

    // ============================================
    // USERS BY ROLE
    // ============================================
    [HttpGet]
    public IActionResult UsersByRole(string rola)
    {
        if (string.IsNullOrWhiteSpace(rola))
            return BadRequest(new { msg = "Brak parametru rola" });

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        rola = rola.Trim();

        using var con = Db.OpenConnection(DbPath);

        long roleId;
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($n) LIMIT 1;";
            cmd.Parameters.AddWithValue("$n", rola);
            var obj = cmd.ExecuteScalar();
            if (obj == null)
                return NotFound(new { msg = "Nie znaleziono roli w tabeli Uprawnienia", rola });
            roleId = Convert.ToInt64(obj);
        }

        var results = new List<UserListRowDto>();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.Email,
       u.pesel,
       CASE WHEN u.Status = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS Status,
       $rola AS Rola
FROM Uzytkownik_Uprawnienia uu
JOIN Uzytkownicy u ON u.id = uu.uzytkownik_id
WHERE uu.uprawnienie_id = $rid
  AND COALESCE(u.czy_zapomniany,0) = 0
ORDER BY u.LastName, u.firstName, u.username;
";
            cmd.Parameters.AddWithValue("$rid", roleId);
            cmd.Parameters.AddWithValue("$rola", rola);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                results.Add(new UserListRowDto
                {
                    Id = Convert.ToInt64(r["id"]),
                    Username = r["username"]?.ToString(),
                    FirstName = r["firstName"]?.ToString(),
                    LastName = r["LastName"]?.ToString(),
                    Email = r["Email"]?.ToString(),
                    Pesel = r["pesel"]?.ToString(),
                    Status = r["Status"]?.ToString(),
                    Rola = r["Rola"]?.ToString()
                });
            }
        }

        ViewBag.Rola = rola;
        return View(results);
    }

    // ============================================
    // NADAJ UPRAWNIENIA (POST)
    // ============================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRole(long id, string rola)
    {
        if (string.IsNullOrWhiteSpace(rola))
            return BadRequest(new { msg = "Brak roli" });

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { msg = "Brak bazy", path = DbPath });

        using var con = Db.OpenConnection(DbPath);

        long permId;
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($n) LIMIT 1;";
            cmd.Parameters.AddWithValue("$n", rola.Trim());
            var obj = cmd.ExecuteScalar();
            if (obj == null) return BadRequest(new { msg = "Nie ma takiego uprawnienia w tabeli Uprawnienia" });
            permId = Convert.ToInt64(obj);
        }

        using (var del = con.CreateCommand())
        {
            del.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uid;";
            del.Parameters.AddWithValue("$uid", id);
            del.ExecuteNonQuery();
        }

        using (var ins = con.CreateCommand())
        {
            ins.CommandText = @"
INSERT INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id)
VALUES ($pid, $uid);
";
            ins.Parameters.AddWithValue("$pid", permId);
            ins.Parameters.AddWithValue("$uid", id);
            ins.ExecuteNonQuery();
        }

        return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
    }
}