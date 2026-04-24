using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using System.Data;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    // Jawny routing: URL będzie dokładnie /Uzytkownicy/UserDetails/1
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
       u.Status AS RawStatus, -- Pobieramy surową wartość statusu
       u.Plec,
       u.DataUrodzenia,
       u.Email,
       u.NrTelefonu,
       u.Miejscowosc,
       u.KodPocztowy,
       u.numer_posesji,
       u.Ulica,
       u.NrLokalu,
       COALESCE(u.czy_zapomniany, 0) AS czy_zapomniany, -- Pobieramy flagę RODO
       COALESCE(GROUP_CONCAT(p.Nazwa, '|'), '') AS RolaRaw -- Używamy separatora | dla łatwiejszego splitu
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

        // Odczytujemy flagę zapomnienia
        bool isForgotten = Convert.ToInt32(dbReader["czy_zapomniany"]) == 1;
        
        // Przygotowanie ról do listy (dla checkboxów w popupie)
        string rolesRaw = dbReader["RolaRaw"]?.ToString() ?? "";
        var roleList = rolesRaw.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

        var userDetails = new UserDetailsDto
        {
            Id = Convert.ToInt64(dbReader["id"]),
            Username = dbReader["username"]?.ToString(),
            FirstName = dbReader["firstName"]?.ToString(),
            LastName = dbReader["LastName"]?.ToString(),
            Pesel = dbReader["pesel"]?.ToString(),
            
            // Logika statusu: jeśli zapomniany, nadpisujemy status
            Status = isForgotten ? "Zanonimizowany" : (Convert.ToInt32(dbReader["RawStatus"]) == 1 ? "Aktywny" : "Nieaktywny"),
            
            Plec = dbReader["Plec"] is DBNull ? 0 : Convert.ToInt32(dbReader["Plec"]),
            DataUrodzenia = dbReader["DataUrodzenia"]?.ToString(),

            Email = dbReader["Email"]?.ToString(),
            NrTelefonu = dbReader["NrTelefonu"]?.ToString(),
            Miejscowosc = dbReader["Miejscowosc"]?.ToString(),
            KodPocztowy = dbReader["KodPocztowy"]?.ToString(),
            Ulica = dbReader["Ulica"]?.ToString(),
            NrPosesji = dbReader["numer_posesji"]?.ToString(),
            NrLokalu = dbReader["NrLokalu"]?.ToString(),

            // Nowe pola obsługujące logikę widoku
            IsForgotten = isForgotten,
            RoleList = roleList,
            Rola = roleList.Any() ? string.Join(", ", roleList) : "-"
        };

        return View(userDetails);
    }
}