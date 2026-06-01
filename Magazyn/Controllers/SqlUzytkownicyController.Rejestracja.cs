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
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($u))";
        command.Parameters.AddWithValue("$u", (username ?? "").Trim());
        
        var count = Convert.ToInt32(command.ExecuteScalar());
        
        if (count > 0)
            return Json("Ta nazwa użytkownika jest już zajęta.");
        
        return Json(true);
    }

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckEmail(string email)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($e))";
        command.Parameters.AddWithValue("$e", (email ?? "").Trim());
        
        var count = Convert.ToInt32(command.ExecuteScalar());
        
        if (count > 0)
            return Json("Ten adres e-mail jest już zarejestrowany.");
        
        return Json(true);
    }

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckPesel(string pesel)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE TRIM(pesel) = TRIM($p)";
        command.Parameters.AddWithValue("$p", (pesel ?? "").Trim());
        
        var count = Convert.ToInt32(command.ExecuteScalar());
        
        if (count > 0)
            return Json("Ten PESEL widnieje już w bazie.");
        
        return Json(true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult Rejestracja(UserRegistrationDto registration)
    {
        if (!ModelState.IsValid)
            return View(registration);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Nie znaleziono bazy danych: {DbPath}");
            return View(registration);
        }

        _logger.LogInformation("[AdminAccess] '{User}' rejestruje nowego użytkownika login='{NewLogin}' IP={RemoteIp}",
            SL(User.Identity?.Name), SL(registration.Username), HttpContext.Connection.RemoteIpAddress);

        registration.Username = (registration.Username ?? "").Trim();
        registration.Password = (registration.Password ?? "").Trim();
        registration.FirstName = (registration.FirstName ?? "").Trim();
        registration.LastName = (registration.LastName ?? "").Trim();
        registration.Pesel = (registration.Pesel ?? "").Trim();
        registration.Email = (registration.Email ?? "").Trim();
        registration.NrTelefonu = (registration.NrTelefonu ?? "").Trim();
        registration.Miejscowosc = (registration.Miejscowosc ?? "").Trim();
        registration.KodPocztowy = (registration.KodPocztowy ?? "").Trim();
        registration.NrPosesji = (registration.NrPosesji ?? "").Trim();
        registration.Ulica = (registration.Ulica ?? "").Trim();
        registration.NrLokalu = (registration.NrLokalu ?? "").Trim();

        if (!TryValidatePeselConsistency(registration.Pesel, registration.DataUrodzenia, registration.Plec, out var peselError))
        {
            ModelState.AddModelError(nameof(registration.Pesel), peselError);
            return View(registration);
        }

        var birthDateString = registration.DataUrodzenia?.ToString("yyyy-MM-dd");

        using var connection = Db.OpenConnection(DbPath);

        using (var checkUsernameCommand = connection.CreateCommand())
        {
            checkUsernameCommand.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($username));";
            checkUsernameCommand.Parameters.AddWithValue("$username", registration.Username);
            if (Convert.ToInt32(checkUsernameCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("Username", "Taki login już istnieje.");
                return View(registration);
            }
        }

        using (var checkEmailCommand = connection.CreateCommand())
        {
            checkEmailCommand.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($email));";
            checkEmailCommand.Parameters.AddWithValue("$email", registration.Email);
            if (Convert.ToInt32(checkEmailCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("Email", "Taki email już istnieje.");
                return View(registration);
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

            insertCommand.Parameters.AddWithValue("$email", registration.Email);
            insertCommand.Parameters.AddWithValue("$firstName", registration.FirstName);
            insertCommand.Parameters.AddWithValue("$username", registration.Username);
            insertCommand.Parameters.AddWithValue("$miejscowosc", registration.Miejscowosc);
            insertCommand.Parameters.AddWithValue("$lastName", registration.LastName);
            insertCommand.Parameters.AddWithValue("$nrLokalu", string.IsNullOrWhiteSpace(registration.NrLokalu) ? DBNull.Value : registration.NrLokalu);
            insertCommand.Parameters.AddWithValue("$pesel", registration.Pesel);
            insertCommand.Parameters.AddWithValue("$plec", PlecToInt(registration.Plec));
            insertCommand.Parameters.AddWithValue("$nrTelefonu", registration.NrTelefonu);
            insertCommand.Parameters.AddWithValue("$ulica", string.IsNullOrWhiteSpace(registration.Ulica) ? DBNull.Value : registration.Ulica);
            insertCommand.Parameters.AddWithValue("$dataUrodzenia", string.IsNullOrWhiteSpace(birthDateString) ? DBNull.Value : birthDateString);
            insertCommand.Parameters.AddWithValue("$password", string.IsNullOrWhiteSpace(registration.Password) ? DBNull.Value : registration.Password);
            insertCommand.Parameters.AddWithValue("$kodPocztowy", registration.KodPocztowy);
            insertCommand.Parameters.AddWithValue("$nrPosesji", registration.NrPosesji);
            insertCommand.Parameters.AddWithValue("$status", StatusToInt(registration.Status));

            insertCommand.ExecuteNonQuery();
        }

        if (!string.IsNullOrWhiteSpace(registration.Rola))
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
                roleIdCommand.Parameters.AddWithValue("$nazwaRoli", registration.Rola.Trim());
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
