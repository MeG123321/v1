using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    // Jawny routing: URL będzie dokładnie /Uzytkownicy/UserDetails/1
    [HttpGet("/Uzytkownicy/UserDetails/{id:long}")]
    public IActionResult UserDetails(long id)
    {
        if (id <= 0)
            return BadRequest(new { msg = "Nieprawidłowe id", id });

        _logger.LogInformation("[AdminAccess] '{User}' przejrzał szczegóły użytkownika id={TargetId} IP={RemoteIp}",
            SL(User.Identity?.Name), id, HttpContext.Connection.RemoteIpAddress);

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.pesel,
       CASE WHEN u.Status = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS Status,
       u.Plec,
       u.DataUrodzenia,
       u.Email,
       u.NrTelefonu,
       u.Miejscowosc,
       u.KodPocztowy,
       u.numer_posesji,
       u.Ulica,
       u.NrLokalu,
       COALESCE(GROUP_CONCAT(p.Nazwa, ', '), '-') AS Rola
FROM Uzytkownicy u
LEFT JOIN Uzytkownik_Uprawnienia uu ON uu.uzytkownik_id = u.id
LEFT JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
WHERE u.id = $id
GROUP BY u.id
LIMIT 1;
";
        command.Parameters.AddWithValue("$id", id);

        using var dbReader = command.ExecuteReader();
        if (!dbReader.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika", id });

        var userDetails = new UserDetailsDto
        {
            Id = Convert.ToInt64(dbReader["id"]),
            Username = dbReader["username"]?.ToString(),
            FirstName = dbReader["firstName"]?.ToString(),
            LastName = dbReader["LastName"]?.ToString(),
            Pesel = dbReader["pesel"]?.ToString(),
            Status = dbReader["Status"]?.ToString(),
            Plec = dbReader["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(dbReader["Plec"]),
            DataUrodzenia = dbReader["DataUrodzenia"]?.ToString(),

            Email = dbReader["Email"]?.ToString(),
            NrTelefonu = dbReader["NrTelefonu"]?.ToString(),
            Miejscowosc = dbReader["Miejscowosc"]?.ToString(),
            KodPocztowy = dbReader["KodPocztowy"]?.ToString(),
            Ulica = dbReader["Ulica"]?.ToString(),
            NrPosesji = dbReader["numer_posesji"]?.ToString(),
            NrLokalu = dbReader["NrLokalu"]?.ToString(),

            Rola = dbReader["Rola"]?.ToString(),
        };

        return View(userDetails);
    }
}