using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using System.Data;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet("/Uzytkownicy/UserDetails/{id:long}")]
    [Authorize(Roles = "Administrator,Kierownik magazynu,Kierownik sprzedazy")]
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
       u.Status AS RawStatus,
       u.Plec,
       u.DataUrodzenia,
       u.Email,
       u.NrTelefonu,
       u.Miejscowosc,
       u.KodPocztowy,
       u.numer_posesji,
       u.Ulica,
       u.NrLokalu,
       COALESCE(u.czy_zapomniany, 0) AS czy_zapomniany,
       COALESCE(GROUP_CONCAT(p.Nazwa, '|'), '') AS RolaRaw
FROM Uzytkownicy u
LEFT JOIN Uzytkownik_Uprawnienia uu ON uu.uzytkownik_id = u.id
LEFT JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
WHERE u.id = $id
GROUP BY u.id
LIMIT 1;
";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika", id });

        bool isForgotten = Convert.ToInt32(reader["czy_zapomniany"]) == 1;
        
        string rawRoles = reader["RolaRaw"]?.ToString() ?? "";
        var roleNames = rawRoles.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

        var userDetails = new UserDetailsDto
        {
            Id = Convert.ToInt64(reader["id"]),
            Username = reader["username"]?.ToString(),
            FirstName = reader["firstName"]?.ToString(),
            LastName = reader["LastName"]?.ToString(),
            Pesel = reader["pesel"]?.ToString(),
            
            Status = isForgotten ? "Zanonimizowany" : (Convert.ToInt32(reader["RawStatus"]) == 1 ? "Aktywny" : "Nieaktywny"),
            
            Plec = reader["Plec"] is DBNull ? 0 : Convert.ToInt32(reader["Plec"]),
            DataUrodzenia = reader["DataUrodzenia"]?.ToString(),

            Email = reader["Email"]?.ToString(),
            NrTelefonu = reader["NrTelefonu"]?.ToString(),
            Miejscowosc = reader["Miejscowosc"]?.ToString(),
            KodPocztowy = reader["KodPocztowy"]?.ToString(),
            Ulica = reader["Ulica"]?.ToString(),
            NrPosesji = reader["numer_posesji"]?.ToString(),
            NrLokalu = reader["NrLokalu"]?.ToString(),

            IsForgotten = isForgotten,
            RoleList = roleNames,
            Rola = roleNames.Any() ? string.Join(", ", roleNames) : "-"
        };

        return View(userDetails);
    }
}
