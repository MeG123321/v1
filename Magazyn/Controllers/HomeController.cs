using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _env;

    public HomeController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string DbPath => Db.GetDbPath(_env);

    public IActionResult Index() => View();

    // =========================
    // LOGOWANIE
    // =========================
    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return BadRequest(new { ok = false, msg = "Brak loginu lub hasła" });

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { ok = false, msg = "Brak bazy", path = DbPath });

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT username
FROM Uzytkownicy
WHERE LOWER(TRIM(username)) = LOWER(TRIM($u))
  AND TRIM(COALESCE(Password,'')) = TRIM($p)
  AND COALESCE(czy_zapomniany,0) = 0
LIMIT 1;
";
        cmd.Parameters.AddWithValue("$u", username.Trim());
        cmd.Parameters.AddWithValue("$p", password.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Unauthorized(new { ok = false, msg = "Błędne dane" });

        return Json(new { ok = true, username = reader["username"]?.ToString() });
    }

    // =========================
    // API: lista userów
    // =========================
    [HttpGet]
    public IActionResult ApiUsers(string? login = null, string? name = null, string? pesel = null)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { error = "Brak pliku bazy", path = DbPath });

        var results = new List<object>();
        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT id, username, firstName, LastName, pesel, Email
FROM Uzytkownicy
WHERE COALESCE(czy_zapomniany,0) = 0
  AND ($login IS NULL OR LOWER(TRIM(username)) LIKE '%' || LOWER(TRIM($login)) || '%')
  AND ($name  IS NULL OR LOWER(TRIM(firstName || ' ' || LastName)) LIKE '%' || LOWER(TRIM($name)) || '%')
  AND ($pesel IS NULL OR TRIM(pesel) LIKE '%' || TRIM($pesel) || '%')
ORDER BY id;
";
        cmd.Parameters.AddWithValue("$login", (object?)login ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pesel", (object?)pesel ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new
            {
                id = reader["id"],
                username = reader["username"],
                firstName = reader["firstName"],
                lastName = reader["LastName"],
                email = reader["Email"],
                pesel = reader["pesel"]
            });
        }

        return Json(results);
    }

    // =========================
    // API: jeden user
    // =========================
    [HttpGet]
    public IActionResult ApiUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
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
        cmd.Parameters.AddWithValue("$id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        return Json(new
        {
            id = r["id"],
            username = r["username"],
            password = r["Password"],
            firstName = r["firstName"],
            lastName = r["LastName"],
            pesel = r["pesel"],
            status = r["Status"],
            plec = r["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(r["Plec"]),
            dataUrodzenia = r["DataUrodzenia"],
            email = r["Email"],
            nrTelefonu = r["NrTelefonu"],
            miejscowosc = r["Miejscowosc"],
            kodPocztowy = r["KodPocztowy"],
            nrPosesji = r["numer_posesji"],
            ulica = r["Ulica"],
            nrLokalu = r["NrLokalu"],
            zapomniany = Convert.ToInt32(r["czy_zapomniany"]) == 1,
            dataZapomnienia = r["DataZapomnienia"],
            zapomnialUserId = r["ZapomnialUserId"]
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
