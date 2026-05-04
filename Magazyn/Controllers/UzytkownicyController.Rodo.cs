using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models.Dtos;
using Magazyn.Security;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator")]
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
                u.id,
                u.firstName,
                u.LastName,
                u.DataZapomnienia,
                u.ZapomnialUserId,
                (a.firstName || ' ' || a.LastName) AS AdminFullName
            FROM Uzytkownicy u
            LEFT JOIN Uzytkownicy a ON u.ZapomnialUserId = a.id
            WHERE u.czy_zapomniany = 1
              AND ($fname = '' OR $fname IS NULL OR (u.firstName || ' ' || u.LastName) LIKE '%' || $fname || '%')
              AND ($adminId IS NULL OR u.ZapomnialUserId = $adminId)
            ORDER BY u.DataZapomnienia DESC;";

        command.Parameters.AddWithValue("$fname", string.IsNullOrWhiteSpace(fname) ? "" : fname.Trim());
        command.Parameters.AddWithValue("$adminId", adminId.HasValue ? adminId.Value : DBNull.Value);

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            var firstName = dbReader.IsDBNull(1) ? "" : dbReader.GetString(1);
            var lastName = dbReader.IsDBNull(2) ? "" : dbReader.GetString(2);

            var adminName = dbReader.IsDBNull(5) ? "Nieznany admin" : dbReader.GetString(5);

            forgottenList.Add(new ForgottenRowDto
            {
                Id = dbReader.GetInt64(0),
                FullNameAfterForget = $"{firstName} {lastName}".Trim(),
                DataZapomnienia = dbReader.IsDBNull(3) ? "" : dbReader.GetString(3),
                ZapomnialUserId = dbReader.IsDBNull(4) ? "" : dbReader.GetInt64(4).ToString(),
                AdminName = adminName
            });
        }

        return View(forgottenList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public IActionResult ForgetUserFromDetails(long id)
    {
        var actionResult = ForgetUser(id);
        if (actionResult is OkObjectResult)
            return RedirectToAction(nameof(AdminPanel));

        return actionResult;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public IActionResult ForgetUser(long id)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(adminIdStr, out var adminId))
            return Unauthorized(new { msg = "Brak adminId w sesji (zaloguj się ponownie)." });

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy" });

        using var connection = Db.OpenConnection(DbPath);

        static int SecureRandomInt(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);

        static string RandomLetters(int length)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz";
            var charBuffer = new char[length];
            for (int i = 0; i < length; i++)
                charBuffer[i] = alphabet[SecureRandomInt(alphabet.Length)];
            return char.ToUpper(charBuffer[0]) + new string(charBuffer, 1, length - 1);
        }

        int anonymizedGender = SecureRandomInt(2);
        int birthYear = 1955 + SecureRandomInt(50);
        int birthMonth = 1 + SecureRandomInt(12);
        int birthDay = 1 + SecureRandomInt(DateTime.DaysInMonth(birthYear, birthMonth));
        var birthDate = new DateTime(birthYear, birthMonth, birthDay);

        string anonymizedFirstName = RandomLetters(6);
        string anonymizedLastName = RandomLetters(8);
        string anonymizedBirthDate = birthDate.ToString("yyyy-MM-dd");
        string anonymizedPesel = GenerateValidPesel(birthDate, anonymizedGender);

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
                username        = $username,
                Password        = $password,
                Status          = 'Nieaktywny'
            WHERE id = $id;";

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$adminId", adminId);
        command.Parameters.AddWithValue("$firstName", anonymizedFirstName);
        command.Parameters.AddWithValue("$lastName", anonymizedLastName);
        command.Parameters.AddWithValue("$pesel", anonymizedPesel);
        command.Parameters.AddWithValue("$dataUrodzenia", anonymizedBirthDate);
        command.Parameters.AddWithValue("$plec", anonymizedGender);

        command.Parameters.AddWithValue("$username", "del_" + PasswordGenerator.RandomToken(8));
        command.Parameters.AddWithValue("$password", PasswordGenerator.GenerateRodoPassword(12));

        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows == 0) return NotFound(new { msg = "Użytkownik nie istnieje" });

        using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uzytkownikId;";
            deleteCmd.Parameters.AddWithValue("$uzytkownikId", id);
            deleteCmd.ExecuteNonQuery();
        }

        return Ok(new { ok = true });
    }

    private static string GenerateValidPesel(DateTime date, int gender)
    {
        int year = date.Year;
        int month = date.Month;
        int day = date.Day;
        if (year >= 2000) month += 20;

        int[] digits = new int[11];
        digits[0] = (year % 100) / 10;
        digits[1] = year % 10;
        digits[2] = month / 10;
        digits[3] = month % 10;
        digits[4] = day / 10;
        digits[5] = day % 10;
        digits[6] = RandomNumberGenerator.GetInt32(10);
        digits[7] = RandomNumberGenerator.GetInt32(10);
        digits[8] = RandomNumberGenerator.GetInt32(10);

        int genderDigit = RandomNumberGenerator.GetInt32(5) * 2;
        if (gender == 1) genderDigit += 1;
        digits[9] = genderDigit;

        int[] weights = { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
        int sum = 0;
        for (int i = 0; i < 10; i++) sum += digits[i] * weights[i];
        digits[10] = (10 - (sum % 10)) % 10;

        return string.Join("", digits);
    }
}
