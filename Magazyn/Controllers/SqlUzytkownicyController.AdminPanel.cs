using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{

    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu,Kierownik sprzedazy")]
    public IActionResult AdminPanel(string? login = null, string? name = null, string? pesel = null)
    {
        _logger.LogInformation("[AdminAccess] '{User}' otworzył AdminPanel [login={Login}, name={Name}, pesel={Pesel}] IP={RemoteIp}",
            SL(User.Identity?.Name), SL(login), SL(name), SL(pesel), HttpContext.Connection.RemoteIpAddress);

        ViewBag.Login = login ?? "";
        ViewBag.Name = name ?? "";
        ViewBag.Pesel = pesel ?? "";

        var users = new List<UserListRowDto>();
        if (!System.IO.File.Exists(DbPath))
            return View(users);

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        command.CommandText = @"
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
WHERE COALESCE(u.czy_zapomniany,0) = 0
  AND ($login IS NULL OR $login = '' OR LOWER(TRIM(u.username)) LIKE '%' || LOWER(TRIM($login)) || '%')
  AND ($name  IS NULL OR $name  = '' OR LOWER(TRIM(u.firstName || ' ' || u.LastName)) LIKE '%' || LOWER(TRIM($name)) || '%')
  AND ($pesel IS NULL OR $pesel = '' OR TRIM(u.pesel) LIKE '%' || TRIM($pesel) || '%')
GROUP BY u.id, u.username, u.firstName, u.LastName, u.Email, u.pesel
ORDER BY u.id;
";
        command.Parameters.AddWithValue("$login", login ?? "");
        command.Parameters.AddWithValue("$name", name ?? "");
        command.Parameters.AddWithValue("$pesel", pesel ?? "");

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new UserListRowDto
            {
                Id = Convert.ToInt64(reader["id"]),
                Username = reader["username"]?.ToString(),
                FirstName = reader["firstName"]?.ToString(),
                LastName = reader["LastName"]?.ToString(),
                Email = reader["Email"]?.ToString(),
                Pesel = reader["pesel"]?.ToString(),
                Status = reader["Status"]?.ToString(),
                Rola = reader["Rola"]?.ToString()
            });
        }

        return View(users);
    }
}