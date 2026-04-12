using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Magazyn.Data;
using Magazyn.Models;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

public class UzytkownicyController : Controller
{
    private readonly IWebHostEnvironment _env;

    public UzytkownicyController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string DbPath => Db.GetDbPath(_env);

    private static string PlecToText(int v) => v == 1 ? "Mężczyzna" : "Kobieta";
    private static int PlecToInt(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return 0;
        return v.Trim().Equals("Mężczyzna", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    // ============================================
    // ADMIN PANEL
    // ============================================
    [HttpGet]
    public IActionResult AdminPanel(string? login = null, string? name = null, string? pesel = null)
    {
        ViewBag.Login = login ?? "";
        ViewBag.Name = name ?? "";
        ViewBag.Pesel = pesel ?? "";

        var results = new List<UserListRowDto>();
        if (!System.IO.File.Exists(DbPath))
            return View(results);

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.Email,
       u.pesel,
       u.Status,
       COALESCE(GROUP_CONCAT(p.Nazwa, ', '), '-') AS Rola
FROM Uzytkownicy u
LEFT JOIN Uzytkownik_Uprawnienia uu ON uu.uzytkownik_id = u.id
LEFT JOIN Uprawnienia p ON p.Id = uu.uprawnienie_id
WHERE COALESCE(u.czy_zapomniany,0) = 0
  AND ($login IS NULL OR $login = '' OR LOWER(TRIM(u.username)) LIKE '%' || LOWER(TRIM($login)) || '%')
  AND ($name  IS NULL OR $name  = '' OR LOWER(TRIM(u.firstName || ' ' || u.LastName)) LIKE '%' || LOWER(TRIM($name)) || '%')
  AND ($pesel IS NULL OR $pesel = '' OR TRIM(u.pesel) LIKE '%' || TRIM($pesel) || '%')
GROUP BY u.id, u.username, u.firstName, u.LastName, u.Email, u.pesel, u.Status
ORDER BY u.id;
";
        cmd.Parameters.AddWithValue("$login", login ?? "");
        cmd.Parameters.AddWithValue("$name", name ?? "");
        cmd.Parameters.AddWithValue("$pesel", pesel ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            results.Add(new UserListRowDto
            {
                Id = Convert.ToInt64(r["id"]),
                Username = r["username"]?.ToString(),
                FirstName = r["firstName"]?.ToString(),
                LastName = r["LastName"]?.ToString(),
                Email = r["Email"]?.ToString(),
                Pesel = r["pesel"]?.ToString(),
                Status = r["Status"]?.ToString(),
                Rola = r["Rola"]?.ToString()
            });
        }

        return View(results);
    }

    // ============================================
    // SZCZEGÓŁY USERA
    // ============================================
    [HttpGet]
    public IActionResult UserDetails(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.pesel,
       u.Status,
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
        cmd.Parameters.AddWithValue("$id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        var u = new UserDetailsDto
        {
            Id = Convert.ToInt64(r["id"]),
            Username = r["username"]?.ToString(),
            FirstName = r["firstName"]?.ToString(),
            LastName = r["LastName"]?.ToString(),
            Pesel = r["pesel"]?.ToString(),
            Status = r["Status"]?.ToString(),
            Plec = r["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(r["Plec"]),
            Rola = r["Rola"]?.ToString(),
            DataUrodzenia = r["DataUrodzenia"]?.ToString(),
            Email = r["Email"]?.ToString(),
            NrTelefonu = r["NrTelefonu"]?.ToString(),
            Miejscowosc = r["Miejscowosc"]?.ToString(),
            KodPocztowy = r["KodPocztowy"]?.ToString(),
            NrPosesji = r["numer_posesji"]?.ToString(),
            Ulica = r["Ulica"]?.ToString(),
            NrLokalu = r["NrLokalu"]?.ToString(),
        };

        return View(u);
    }

    // ============================================
    // REJESTRACJA
    // ============================================
    [HttpGet]
    public IActionResult Rejestracja() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Rejestracja(UserRegistrationDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Nie znaleziono bazy danych: {DbPath}");
            return View(dto);
        }

        dto.Username = (dto.Username ?? "").Trim();
        dto.Password = (dto.Password ?? "").Trim();
        dto.FirstName = (dto.FirstName ?? "").Trim();
        dto.LastName = (dto.LastName ?? "").Trim();
        dto.Pesel = (dto.Pesel ?? "").Trim();
        dto.Status = (dto.Status ?? "").Trim();
        dto.Plec = (dto.Plec ?? "").Trim();
        dto.DataUrodzenia = (dto.DataUrodzenia ?? "").Trim();
        dto.Email = (dto.Email ?? "").Trim();
        dto.NrTelefonu = (dto.NrTelefonu ?? "").Trim();
        dto.Miejscowosc = (dto.Miejscowosc ?? "").Trim();
        dto.KodPocztowy = (dto.KodPocztowy ?? "").Trim();
        dto.NrPosesji = (dto.NrPosesji ?? "").Trim();
        dto.Ulica = (dto.Ulica ?? "").Trim();
        dto.NrLokalu = (dto.NrLokalu ?? "").Trim();

        using var con = Db.OpenConnection(DbPath);

        using (var check = con.CreateCommand())
        {
            check.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($u));";
            check.Parameters.AddWithValue("$u", dto.Username);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("", "Taki login już istnieje.");
                return View(dto);
            }
        }

        using (var checkEmail = con.CreateCommand())
        {
            checkEmail.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($e));";
            checkEmail.Parameters.AddWithValue("$e", dto.Email);
            if (Convert.ToInt32(checkEmail.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("", "Taki email już istnieje.");
                return View(dto);
            }
        }

        using (var checkPesel = con.CreateCommand())
        {
            checkPesel.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE TRIM(pesel) = TRIM($p);";
            checkPesel.Parameters.AddWithValue("$p", dto.Pesel);
            if (Convert.ToInt32(checkPesel.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("", "Taki PESEL już istnieje.");
                return View(dto);
            }
        }

        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO Uzytkownicy
(Email, firstName, username, Miejscowosc, LastName, NrLokalu, pesel, Plec, NrTelefonu, Ulica,
 blokada_do, czy_zapomniany, DataUrodzenia, DataZapomnienia, Password, KodPocztowy,
 liczba_blednych_logowan, numer_posesji, ZapomnialUserId, Status)
VALUES
($Email, $FirstName, $Username, $Miejscowosc, $LastName, $NrLokalu, $Pesel, $Plec, $NrTelefonu, $Ulica,
 NULL, 0, $DataUrodzenia, NULL, $Password, $KodPocztowy,
 0, $NrPosesji, NULL, $Status);
";
            cmd.Parameters.AddWithValue("$Email", dto.Email);
            cmd.Parameters.AddWithValue("$FirstName", dto.FirstName);
            cmd.Parameters.AddWithValue("$Username", dto.Username);
            cmd.Parameters.AddWithValue("$Miejscowosc", dto.Miejscowosc);
            cmd.Parameters.AddWithValue("$LastName", dto.LastName);
            cmd.Parameters.AddWithValue("$NrLokalu", string.IsNullOrWhiteSpace(dto.NrLokalu) ? DBNull.Value : dto.NrLokalu);
            cmd.Parameters.AddWithValue("$Pesel", dto.Pesel);
            cmd.Parameters.AddWithValue("$Plec", PlecToInt(dto.Plec));
            cmd.Parameters.AddWithValue("$NrTelefonu", dto.NrTelefonu);
            cmd.Parameters.AddWithValue("$Ulica", string.IsNullOrWhiteSpace(dto.Ulica) ? DBNull.Value : dto.Ulica);
            cmd.Parameters.AddWithValue("$DataUrodzenia", dto.DataUrodzenia);
            cmd.Parameters.AddWithValue("$Password", string.IsNullOrWhiteSpace(dto.Password) ? DBNull.Value : dto.Password);
            cmd.Parameters.AddWithValue("$KodPocztowy", dto.KodPocztowy);
            cmd.Parameters.AddWithValue("$NrPosesji", dto.NrPosesji);
            cmd.Parameters.AddWithValue("$Status", string.IsNullOrWhiteSpace(dto.Status) ? "Aktywny" : dto.Status);
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction(nameof(AdminPanel));
    }

    // ============================================
    // EDYCJA USERA
    // ============================================
    [HttpGet]
    public IActionResult EditUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT id, username, Password, firstName, LastName, pesel, Status, Plec, DataUrodzenia,
       Email, NrTelefonu,
       Miejscowosc, KodPocztowy, numer_posesji, Ulica, NrLokalu
FROM Uzytkownicy
WHERE id = $id
LIMIT 1;
";
        cmd.Parameters.AddWithValue("$id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return NotFound(new { msg = "Nie znaleziono użytkownika" });

        var vm = new UserVm
        {
            Id = Convert.ToInt64(r["id"]),
            Username = r["username"]?.ToString() ?? "",
            Password = r["Password"]?.ToString() ?? "",
            FirstName = r["firstName"]?.ToString() ?? "",
            LastName = r["LastName"]?.ToString() ?? "",
            Pesel = r["pesel"]?.ToString() ?? "",
            Status = r["Status"]?.ToString() ?? "",
            Plec = PlecToText(r["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(r["Plec"])),
            DataUrodzenia = r["DataUrodzenia"]?.ToString() ?? "",
            Email = r["Email"]?.ToString() ?? "",
            NrTelefonu = r["NrTelefonu"]?.ToString() ?? "",
            Miejscowosc = r["Miejscowosc"]?.ToString() ?? "",
            KodPocztowy = r["KodPocztowy"]?.ToString() ?? "",
            NrPosesji = r["numer_posesji"]?.ToString() ?? "",
            Ulica = r["Ulica"]?.ToString(),
            NrLokalu = r["NrLokalu"]?.ToString()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditUser(UserVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Nie znaleziono bazy danych: {DbPath}");
            return View(vm);
        }

        vm.Username = (vm.Username ?? "").Trim();
        vm.Password = (vm.Password ?? "").Trim();
        vm.FirstName = (vm.FirstName ?? "").Trim();
        vm.LastName = (vm.LastName ?? "").Trim();
        vm.Pesel = (vm.Pesel ?? "").Trim();
        vm.Status = (vm.Status ?? "").Trim();
        vm.Plec = (vm.Plec ?? "").Trim();
        vm.DataUrodzenia = (vm.DataUrodzenia ?? "").Trim();
        vm.Email = (vm.Email ?? "").Trim();
        vm.NrTelefonu = (vm.NrTelefonu ?? "").Trim();
        vm.Miejscowosc = (vm.Miejscowosc ?? "").Trim();
        vm.KodPocztowy = (vm.KodPocztowy ?? "").Trim();
        vm.NrPosesji = (vm.NrPosesji ?? "").Trim();
        vm.Ulica = (vm.Ulica ?? "").Trim();
        vm.NrLokalu = (vm.NrLokalu ?? "").Trim();

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
UPDATE Uzytkownicy
SET username = $Username,
    Password = $Password,
    firstName = $FirstName,
    LastName = $LastName,
    pesel = $Pesel,
    Status = $Status,
    Plec = $Plec,
    DataUrodzenia = $DataUrodzenia,
    Email = $Email,
    NrTelefonu = $NrTelefonu,
    Miejscowosc = $Miejscowosc,
    KodPocztowy = $KodPocztowy,
    numer_posesji = $NrPosesji,
    Ulica = $Ulica,
    NrLokalu = $NrLokalu
WHERE id = $Id;
";
        cmd.Parameters.AddWithValue("$Id", vm.Id);
        cmd.Parameters.AddWithValue("$Username", vm.Username);
        cmd.Parameters.AddWithValue("$Password", string.IsNullOrWhiteSpace(vm.Password) ? DBNull.Value : vm.Password);
        cmd.Parameters.AddWithValue("$FirstName", vm.FirstName);
        cmd.Parameters.AddWithValue("$LastName", vm.LastName);
        cmd.Parameters.AddWithValue("$Pesel", vm.Pesel);
        cmd.Parameters.AddWithValue("$Status", vm.Status);
        cmd.Parameters.AddWithValue("$Plec", PlecToInt(vm.Plec));
        cmd.Parameters.AddWithValue("$DataUrodzenia", vm.DataUrodzenia);
        cmd.Parameters.AddWithValue("$Email", vm.Email);
        cmd.Parameters.AddWithValue("$NrTelefonu", vm.NrTelefonu);
        cmd.Parameters.AddWithValue("$Miejscowosc", vm.Miejscowosc);
        cmd.Parameters.AddWithValue("$KodPocztowy", vm.KodPocztowy);
        cmd.Parameters.AddWithValue("$NrPosesji", vm.NrPosesji);
        cmd.Parameters.AddWithValue("$Ulica", string.IsNullOrWhiteSpace(vm.Ulica) ? DBNull.Value : vm.Ulica);
        cmd.Parameters.AddWithValue("$NrLokalu", string.IsNullOrWhiteSpace(vm.NrLokalu) ? DBNull.Value : vm.NrLokalu);

        var rows = cmd.ExecuteNonQuery();
        if (rows == 0)
        {
            ModelState.AddModelError("", "Nie znaleziono użytkownika.");
            return View(vm);
        }

        return RedirectToAction(nameof(UserDetails), new { id = vm.Id });
    }

    // ============================================
    // FORGOTTEN USERS (RODO)
    // ============================================
    [HttpGet]
    public IActionResult ForgottenUsers(string? fname = null, long? adminId = null)
    {
        ViewBag.Fname = fname ?? "";
        ViewBag.AdminId = adminId?.ToString() ?? "";

        var users = new List<ForgottenRowDto>();

        if (!System.IO.File.Exists(DbPath))
            return View(users);

        using var con = Db.OpenConnection(DbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
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
        cmd.Parameters.AddWithValue("$fname", string.IsNullOrWhiteSpace(fname) ? "" : fname.Trim());
        cmd.Parameters.AddWithValue("$adminId", adminId.HasValue ? adminId.Value : DBNull.Value);

        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var firstName = rd.IsDBNull(1) ? "" : rd.GetString(1);
            var lastName = rd.IsDBNull(2) ? "" : rd.GetString(2);
            users.Add(new ForgottenRowDto
            {
                Id = rd.GetInt64(0),
                FullNameAfterForget = $"{firstName} {lastName}".Trim(),
                DataZapomnienia = rd.IsDBNull(3) ? "" : rd.GetString(3),
                ZapomnialUserId = rd.IsDBNull(4) ? "" : rd.GetInt64(4).ToString()
            });
        }

        return View(users);
    }

    // ============================================
    // ZAPOMNIJ z widoku szczegółów
    // ============================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgetUserFromDetails(long id, long adminId)
    {
        var result = ForgetUser(id, adminId);
        if (result is OkObjectResult)
            return RedirectToAction(nameof(AdminPanel));
        return result;
    }

    // ============================================
    // RODO: zapomnienie usera
    // ============================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgetUser(long id, long adminId)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = Db.OpenConnection(DbPath);

        static int SecureNext(int maxExclusive) =>
            RandomNumberGenerator.GetInt32(maxExclusive);

        static string RandDigits(int len)
        {
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)('0' + SecureNext(10));
            return new string(chars);
        }

        static string RandLetters(int len)
        {
            const string a = "abcdefghijklmnopqrstuvwxyz";
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = a[SecureNext(a.Length)];
            return char.ToUpper(chars[0]) + new string(chars, 1, len - 1);
        }

        static string RandToken(int len)
        {
            const string a = "abcdefghijklmnopqrstuvwxyz0123456789_";
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = a[SecureNext(a.Length)];
            return new string(chars);
        }

        var newFirst = RandLetters(6);
        var newLast = RandLetters(8);
        var newPesel = RandDigits(11);

        var year = 1950 + SecureNext(56); // 1950-2005
        var month = 1 + SecureNext(12);
        var day = 1 + SecureNext(DateTime.DaysInMonth(year, month));
        var newDob = new DateTime(year, month, day).ToString("yyyy-MM-dd");

        var newPlec = SecureNext(2);
        var newUsername = "del_" + RandToken(10);
        var newPassword = RandToken(12);
        var newEmail = $"{RandToken(8)}@example.com";

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
UPDATE Uzytkownicy
SET czy_zapomniany = 1,
    DataZapomnienia = datetime('now'),
    ZapomnialUserId = $AdminId,
    firstName = $FirstName,
    LastName = $LastName,
    pesel = $Pesel,
    DataUrodzenia = $DataUrodzenia,
    Plec = $Plec,
    Status = 'Nieaktywny',
    username = $Username,
    Password = $Password,
    Email = $Email
WHERE id = $Id;
";
        cmd.Parameters.AddWithValue("$Id", id);
        cmd.Parameters.AddWithValue("$AdminId", adminId);
        cmd.Parameters.AddWithValue("$FirstName", newFirst);
        cmd.Parameters.AddWithValue("$LastName", newLast);
        cmd.Parameters.AddWithValue("$Pesel", newPesel);
        cmd.Parameters.AddWithValue("$DataUrodzenia", newDob);
        cmd.Parameters.AddWithValue("$Plec", newPlec);
        cmd.Parameters.AddWithValue("$Username", newUsername);
        cmd.Parameters.AddWithValue("$Password", newPassword);
        cmd.Parameters.AddWithValue("$Email", newEmail);

        var rows = cmd.ExecuteNonQuery();
        if (rows == 0) return NotFound(new { msg = "Nie znaleziono użytkownika" });

        using (var del = con.CreateCommand())
        {
            del.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uid;";
            del.Parameters.AddWithValue("$uid", id);
            del.ExecuteNonQuery();
        }

        return Ok(new { ok = true });
    }
}
