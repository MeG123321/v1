using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IWebHostEnvironment env, ILogger<HomeController> logger)
    {
        _env = env;
        _logger = logger;
    }

    private string DbPath => Db.GetDbPath(_env);

    private static string StatusToText(object dbValue)
    {
        if (dbValue == DBNull.Value) return "Nieaktywny";
        return Convert.ToInt32(dbValue) == 1 ? "Aktywny" : "Nieaktywny";
    }

    public IActionResult Index() => View();

    [Authorize]
    [HttpGet]
    public IActionResult ApiUsers(string? login = null, string? name = null, string? pesel = null)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { error = "Brak pliku bazy", path = DbPath });

        var users = new List<object>();

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

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

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new
            {
                id        = reader["id"],
                username  = reader["username"],
                firstName = reader["firstName"],
                lastName  = reader["LastName"],
                email     = reader["Email"],
                pesel     = reader["pesel"]
            });
        }

        return Json(users);
    }

    [Authorize]
    [HttpGet]
    public IActionResult ApiUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

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

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        var statusInt = reader["Status"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Status"]);

        return Json(new
        {
            id           = reader["id"],
            username     = reader["username"],
            password     = reader["Password"],
            firstName    = reader["firstName"],
            lastName     = reader["LastName"],
            pesel        = reader["pesel"],

            statusInt    = statusInt,
            status       = StatusToText(reader["Status"]),

            plec         = reader["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Plec"]),
            dataUrodzenia= reader["DataUrodzenia"],
            email        = reader["Email"],
            nrTelefonu   = reader["NrTelefonu"],
            miejscowosc  = reader["Miejscowosc"],
            kodPocztowy  = reader["KodPocztowy"],
            nrPosesji    = reader["numer_posesji"],
            ulica        = reader["Ulica"],
            nrLokalu     = reader["NrLokalu"],
            zapomniany   = Convert.ToInt32(reader["czy_zapomniany"]) == 1,
            dataZapomnienia  = reader["DataZapomnienia"],
            zapomnialUserId  = reader["ZapomnialUserId"]
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
