using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet]
    public IActionResult ForgottenUsers(string? fname = null, long? adminId = null)
    {
        ViewBag.Fname = fname ?? "";
        ViewBag.AdminId = adminId?.ToString() ?? "";

        var forgottenList = new List<ForgottenRowDto>();

        if (!System.IO.File.Exists(DbPath))
            return View(forgottenList);

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT
    id,
    firstName,
    LastName,
    DataZapomnienia,
    ZapomnialUserId
FROM Uzytkownicy
WHERE czy_zapomniany = 1
  AND ($fname = '' OR $fname IS NULL OR (firstName || ' ' || LastName) LIKE '%' || $fname || '%')
  AND ($adminId IS NULL OR ZapomnialUserId = $adminId)
ORDER BY DataZapomnienia DESC;
";
        command.Parameters.AddWithValue("$fname", string.IsNullOrWhiteSpace(fname) ? "" : fname.Trim());
        command.Parameters.AddWithValue("$adminId", adminId.HasValue ? adminId.Value : DBNull.Value);

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            var firstName = dbReader.IsDBNull(1) ? "" : dbReader.GetString(1);
            var lastName = dbReader.IsDBNull(2) ? "" : dbReader.GetString(2);
            forgottenList.Add(new ForgottenRowDto
            {
                Id = dbReader.GetInt64(0),
                FullNameAfterForget = $"{firstName} {lastName}".Trim(),
                DataZapomnienia = dbReader.IsDBNull(3) ? "" : dbReader.GetString(3),
                ZapomnialUserId = dbReader.IsDBNull(4) ? "" : dbReader.GetInt64(4).ToString()
            });
        }

        return View(forgottenList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgetUserFromDetails(long id)
    {
        var actionResult = ForgetUser(id);
        if (actionResult is OkObjectResult)
            return RedirectToAction(nameof(AdminPanel));
        return actionResult;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgetUser(long id)
    {
        // adminId bierzemy z zalogowanego użytkownika
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(adminIdStr, out var adminId))
            return Unauthorized(new { msg = "Brak adminId w sesji (zaloguj się ponownie)." });

        _logger.LogWarning("[AdminAccess] '{User}' wykonuje RODO-zapomnienie użytkownika id={TargetId} (adminId={AdminId}) IP={RemoteIp}",
            SL(User.Identity?.Name), id, adminId, HttpContext.Connection.RemoteIpAddress);

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);

        static int SecureRandomInt(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);

        static string RandomDigits(int length)
        {
            var charBuffer = new char[length];
            for (int i = 0; i < length; i++)
                charBuffer[i] = (char)('0' + SecureRandomInt(10));
            return new string(charBuffer);
        }

        static string RandomLetters(int length)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz";
            var charBuffer = new char[length];
            for (int i = 0; i < length; i++)
                charBuffer[i] = alphabet[SecureRandomInt(alphabet.Length)];
            return char.ToUpper(charBuffer[0]) + new string(charBuffer, 1, length - 1);
        }

        static string RandomToken(int length)
        {
            const string tokenAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789_";
            var charBuffer = new char[length];
            for (int i = 0; i < length; i++)
                charBuffer[i] = tokenAlphabet[SecureRandomInt(tokenAlphabet.Length)];
            return new string(charBuffer);
        }

        var anonymizedFirstName = RandomLetters(6);
        var anonymizedLastName = RandomLetters(8);
        var anonymizedPesel = RandomDigits(11);

        var birthYear = 1950 + SecureRandomInt(56);
        var birthMonth = 1 + SecureRandomInt(12);
        var birthDay = 1 + SecureRandomInt(DateTime.DaysInMonth(birthYear, birthMonth));
        var anonymizedBirthDate = new DateTime(birthYear, birthMonth, birthDay).ToString("yyyy-MM-dd");

        var anonymizedGender = SecureRandomInt(2);
        var anonymizedUsername = "del_" + RandomToken(10);
        var anonymizedPassword = RandomToken(12);
        var anonymizedEmail = $"{RandomToken(8)}@example.com";

        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE Uzytkownicy
SET czy_zapomniany = 1,
    DataZapomnienia = datetime('now'),
    ZapomnialUserId = $adminId,
    firstName       = $firstName,
    LastName        = $lastName,
    pesel           = $pesel,
    DataUrodzenia   = $dataUrodzenia,
    Plec            = $plec,
    Status          = 0,
    username        = $username,
    Password        = $password,
    Email           = $email
WHERE id = $id;
";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$adminId", adminId);
        command.Parameters.AddWithValue("$firstName", anonymizedFirstName);
        command.Parameters.AddWithValue("$lastName", anonymizedLastName);
        command.Parameters.AddWithValue("$pesel", anonymizedPesel);
        command.Parameters.AddWithValue("$dataUrodzenia", anonymizedBirthDate);
        command.Parameters.AddWithValue("$plec", anonymizedGender);
        command.Parameters.AddWithValue("$username", anonymizedUsername);
        command.Parameters.AddWithValue("$password", anonymizedPassword);
        command.Parameters.AddWithValue("$email", anonymizedEmail);

        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows == 0)
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        using (var deletePermissionsCommand = connection.CreateCommand())
        {
            deletePermissionsCommand.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uzytkownikId;";
            deletePermissionsCommand.Parameters.AddWithValue("$uzytkownikId", id);
            deletePermissionsCommand.ExecuteNonQuery();
        }

        return Ok(new { ok = true });
    }
}