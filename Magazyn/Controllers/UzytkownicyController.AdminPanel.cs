using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{

    [HttpGet]
    public IActionResult AdminPanel(string? login = null, string? name = null, string? pesel = null)
    {
        _logger.LogInformation("[AdminAccess] '{User}' otworzył AdminPanel [login={Login}, name={Name}, pesel={Pesel}] IP={RemoteIp}",
            SL(User.Identity?.Name), SL(login), SL(name), SL(pesel), HttpContext.Connection.RemoteIpAddress);

        ViewBag.Login = login ?? "";
        ViewBag.Name = name ?? "";
        ViewBag.Pesel = pesel ?? "";

        var userList = new List<UserListRowDto>();
        if (!System.IO.File.Exists(DbPath))
            return View(userList);

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

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            userList.Add(new UserListRowDto
            {
                Id = Convert.ToInt64(dbReader["id"]),
                Username = dbReader["username"]?.ToString(),
                FirstName = dbReader["firstName"]?.ToString(),
                LastName = dbReader["LastName"]?.ToString(),
                Email = dbReader["Email"]?.ToString(),
                Pesel = dbReader["pesel"]?.ToString(),
                Status = dbReader["Status"]?.ToString(),
                Rola = dbReader["Rola"]?.ToString()
            });
        }

        return View(userList);
    }
}