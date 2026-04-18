using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet]
    public IActionResult Rejestracja() => View();

    // --- METODY WALIDACJI ZDALNEJ (Remote Validation) ---
    // Te metody są wywoływane przez JavaScript, gdy użytkownik wpisuje dane w formularzu.

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckUsername(string Username) // Nazwa musi pasować do pola w DTO
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        // Sprawdzamy bez względu na wielkość liter i usuwamy zbędne spacje
        cmd.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($u))";
        cmd.Parameters.AddWithValue("$u", (Username ?? "").Trim());
        
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count > 0)
            return Json("Ta nazwa użytkownika jest już zajęta.");
        
        return Json(true);
    }

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckEmail(string Email)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($e))";
        cmd.Parameters.AddWithValue("$e", (Email ?? "").Trim());
        
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count > 0)
            return Json("Ten adres e-mail jest już zarejestrowany.");
        
        return Json(true);
    }

    [AcceptVerbs("Get", "Post")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult CheckPesel(string Pesel)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Uzytkownicy WHERE TRIM(pesel) = TRIM($p)";
        cmd.Parameters.AddWithValue("$p", (Pesel ?? "").Trim());
        
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        
        if (count > 0)
            return Json("Ten PESEL widnieje już w bazie.");
        
        return Json(true);
    }

    // --- GŁÓWNA METODA ZAPISU FORMULARZA ---

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Rejestracja(UserRegistrationDto dto)
    {
        // 1. Walidacja modelu (na podstawie atrybutów w DTO)
        if (!ModelState.IsValid)
            return View(dto);

        // 2. Sprawdzenie czy plik bazy istnieje
        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Nie znaleziono bazy danych: {DbPath}");
            return View(dto);
        }

        _logger.LogInformation("[AdminAccess] '{User}' rejestruje nowego użytkownika login='{NewLogin}' IP={RemoteIp}",
            SL(User.Identity?.Name), SL(dto.Username), HttpContext.Connection.RemoteIpAddress);

        // 3. Czyszczenie danych (Trim)
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

        var dataUrodzeniaStr = dto.DataUrodzenia?.ToString("yyyy-MM-dd");

        using var connection = Db.OpenConnection(DbPath);

        // 4. Ponowne sprawdzenie unikalności (zabezpieczenie serwerowe)
        // Sprawdzanie loginu
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

        // Sprawdzanie e-maila
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

        // 5. Zapis nowego użytkownika
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
            insertCommand.Parameters.AddWithValue("$dataUrodzenia", string.IsNullOrWhiteSpace(dataUrodzeniaStr) ? DBNull.Value : dataUrodzeniaStr);
            insertCommand.Parameters.AddWithValue("$password", string.IsNullOrWhiteSpace(dto.Password) ? DBNull.Value : dto.Password);
            insertCommand.Parameters.AddWithValue("$kodPocztowy", dto.KodPocztowy);
            insertCommand.Parameters.AddWithValue("$nrPosesji", dto.NrPosesji);
            insertCommand.Parameters.AddWithValue("$status", StatusToInt(dto.Status));

            insertCommand.ExecuteNonQuery();
        }

        // 6. Nadanie roli
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