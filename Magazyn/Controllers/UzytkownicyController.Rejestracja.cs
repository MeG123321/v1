using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult Rejestracja() => View();

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckUsername(string username)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($u))";
        cmd.Parameters.AddWithValue("$u", (username ?? "").Trim());
        
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count > 0)
            return Json("Ta nazwa użytkownika jest już zajęta.");
        
        return Json(true);
    }

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckEmail(string email)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($e))";
        cmd.Parameters.AddWithValue("$e", (email ?? "").Trim());
        
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count > 0)
            return Json("Ten adres e-mail jest już zarejestrowany.");
        
        return Json(true);
    }

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckPesel(string pesel)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE TRIM(pesel) = TRIM($p)";
        cmd.Parameters.AddWithValue("$p", (pesel ?? "").Trim());
        
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count > 0)
            return Json("Ten PESEL widnieje już w bazie.");
        
        return Json(true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult Rejestracja(UserRegistrationDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Nie znaleziono bazy danych: {DbPath}");
            return View(dto);
        }

        _logger.LogInformation("[AdminAccess] '{User}' rejestruje nowego użytkownika login='{NewLogin}' IP={RemoteIp}",
            SL(User.Identity?.Name), SL(dto.Username), HttpContext.Connection.RemoteIpAddress);

        dto.Username = (dto.Username ?? "").Trim();
        dto.Password = (dto.Password ?? "").Trim();
        dto.FirstName = (dto.FirstName ?? "").Trim();
        dto.LastName = (dto.LastName ?? "").Trim();
        dto.Pesel = (dto.Pesel ?? "").Trim();
        dto.Email = (dto.Email ?? "").Trim();
        dto.NrTelefonu = (dto.NrTelefonu ?? "").Trim();
        dto.Miejscowosc = (dto.Miejscowosc ?? "").Trim();
        dto.KodPocztowy = (dto.KodPocztowy ?? "").Trim();
        dto.NrPosesji = (dto.NrPosesji ?? "").Trim();
        dto.Ulica = (dto.Ulica ?? "").Trim();
        dto.NrLokalu = (dto.NrLokalu ?? "").Trim();

        if (!TryValidatePeselConsistency(dto.Pesel, dto.DataUrodzenia, dto.Plec, out var peselError))
        {
            ModelState.AddModelError(nameof(dto.Pesel), peselError);
            return View(dto);
        }

        var birthDateString = dto.DataUrodzenia?.ToString("yyyy-MM-dd");

        using var connection = Db.OpenConnection(DbPath);

        using (var checkUsernameCommand = connection.CreateCommand())
        {
            checkUsernameCommand.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($username));";
            checkUsernameCommand.Parameters.AddWithValue("$username", dto.Username);
            if (Convert.ToInt32(checkUsernameCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("Username", "Taki login już istnieje.");
                return View(dto);
            }
        }

        using (var checkEmailCommand = connection.CreateCommand())
        {
            checkEmailCommand.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($email));";
            checkEmailCommand.Parameters.AddWithValue("$email", dto.Email);
            if (Convert.ToInt32(checkEmailCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("Email", "Taki email już istnieje.");
                return View(dto);
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = @"
                INSERT INTO Uzytkownicy
                    (Email, firstName, username, Miejscowosc, LastName, NrLokalu, pesel, Plec, NrTelefonu, Ulica,
                     blokada_do, czy_zapomniany, DataUrodzenia, DataZapomnienia, Password, KodPocztowy,
                     liczba_blednych_logowan, numer_posesji, ZapomnialUserId, Status)
                VALUES
                    ($email, $firstName, $username, $miejscowosc, $lastName, $nrLokalu, $pesel, $plec, $nrTelefonu, $ulica,
                     NULL, 0, $dataUrodzenia, NULL, $password, $kodPocztowy,
                     0, $nrPosesji, NULL, $status);";

            insertCommand.Parameters.AddWithValue("$email", dto.Email);
            insertCommand.Parameters.AddWithValue("$firstName", dto.FirstName);
            insertCommand.Parameters.AddWithValue("$username", dto.Username);
            insertCommand.Parameters.AddWithValue("$miejscowosc", dto.Miejscowosc);
            insertCommand.Parameters.AddWithValue("$lastName", dto.LastName);
            insertCommand.Parameters.AddWithValue("$nrLokalu", string.IsNullOrWhiteSpace(dto.NrLokalu) ? DBNull.Value : dto.NrLokalu);
            insertCommand.Parameters.AddWithValue("$pesel", dto.Pesel);
            insertCommand.Parameters.AddWithValue("$plec", PlecToInt(dto.Plec));
            insertCommand.Parameters.AddWithValue("$nrTelefonu", dto.NrTelefonu);
            insertCommand.Parameters.AddWithValue("$ulica", string.IsNullOrWhiteSpace(dto.Ulica) ? DBNull.Value : dto.Ulica);
            insertCommand.Parameters.AddWithValue("$dataUrodzenia", string.IsNullOrWhiteSpace(birthDateString) ? DBNull.Value : birthDateString);
            insertCommand.Parameters.AddWithValue("$password", string.IsNullOrWhiteSpace(dto.Password) ? DBNull.Value : dto.Password);
            insertCommand.Parameters.AddWithValue("$kodPocztowy", dto.KodPocztowy);
            insertCommand.Parameters.AddWithValue("$nrPosesji", dto.NrPosesji);
            insertCommand.Parameters.AddWithValue("$status", StatusToInt(dto.Status));

            insertCommand.ExecuteNonQuery();
        }

        if (!string.IsNullOrWhiteSpace(dto.Rola))
        {
            long newUserId;
            using (var lastIdCommand = connection.CreateCommand())
            {
                lastIdCommand.CommandText = "SELECT last_insert_rowid();";
                newUserId = Convert.ToInt64(lastIdCommand.ExecuteScalar());
            }

            using (var roleIdCommand = connection.CreateCommand())
            {
                roleIdCommand.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($nazwaRoli) LIMIT 1;";
                roleIdCommand.Parameters.AddWithValue("$nazwaRoli", dto.Rola.Trim());
                var roleIdScalar = roleIdCommand.ExecuteScalar();
                if (roleIdScalar != null)
                {
                    var roleId = Convert.ToInt64(roleIdScalar);
                    using var insertRoleCommand = connection.CreateCommand();
                    insertRoleCommand.CommandText = @"INSERT OR IGNORE INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id) VALUES ($roleId, $userId);";
                    insertRoleCommand.Parameters.AddWithValue("$roleId", roleId);
                    insertRoleCommand.Parameters.AddWithValue("$userId", newUserId);
                    insertRoleCommand.ExecuteNonQuery();
                }
            }
        }

        return RedirectToAction(nameof(AdminPanel));
    }
}
