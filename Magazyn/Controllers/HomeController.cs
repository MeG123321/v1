using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Magazyn.Models;

namespace Magazyn.Controllers;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _env;

    public HomeController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string DbPath => Path.Combine(_env.WebRootPath, "magazyn.db");

    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
    public IActionResult Uprawnienia() => View();

    // ============================================
    // DTO: lista (najważniejsze pola)
    // ============================================
    public class UserListRowDto
    {
        public long Id { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Pesel { get; set; }
        public string? Status { get; set; }
        public string? Rola { get; set; } // z JOIN (Uprawnienia), nie z Uzytkownicy
    }

    // ============================================
    // DTO: szczegóły (pełne)
    // ============================================
    public class UserDetailsDto
    {
        public long Id { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Pesel { get; set; }
        public string? DataUrodzenia { get; set; }
        public string? NrTelefonu { get; set; }
        public int Plec { get; set; } // w DB INTEGER
        public string? Status { get; set; }
        public string? Rola { get; set; } // z JOIN
        public string? Miejscowosc { get; set; }
        public string? KodPocztowy { get; set; }
        public string? Ulica { get; set; }
        public string? NrPosesji { get; set; } // numer_posesji
        public string? NrLokalu { get; set; }
    }

    private static string PlecToText(int v) => v == 1 ? "Mężczyzna" : "Kobieta";
    private static int PlecToInt(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return 0;
        return v.Trim().Equals("Mężczyzna", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    // ============================================
    // ADMIN PANEL - SERWEROWO (bez JS)
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

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

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
// lista ról do popupów
var roles = new List<string>();
using (var rc = con.CreateCommand())
{
    rc.CommandText = @"SELECT Nazwa FROM Uprawnienia ORDER BY Id;";
    using var rr = rc.ExecuteReader();
    while (rr.Read())
        roles.Add(rr["Nazwa"]?.ToString() ?? "");
}
ViewBag.Roles = roles;
        return View(results);
    }

    // ============================================
    // SZCZEGÓŁY USERA - SERWEROWO (bez JS)
    // ============================================
    [HttpGet]
    public IActionResult UserDetails(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

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

        var plecInt = Convert.ToInt32(r["Plec"]);

        var u = new UserDetailsDto
        {
            Id = Convert.ToInt64(r["id"]),
            Username = r["username"]?.ToString(),
            FirstName = r["firstName"]?.ToString(),
            LastName = r["LastName"]?.ToString(),
            Pesel = r["pesel"]?.ToString(),
            Status = r["Status"]?.ToString(),
            Plec = plecInt,
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

    // ===== RODO widok =====
    [HttpGet]
    public IActionResult ForgottenUsers() => View();

    // =========================
    // REJESTRACJA (dopasowana do nowej bazy)
    // UWAGA: rola/uprawnienie nadajesz przez tabelę Uzytkownik_Uprawnienia po rejestracji (osobno).
    // =========================
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

        // trim
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

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

        // Username unique
        using (var check = con.CreateCommand())
        {
            check.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($u));";
            check.Parameters.AddWithValue("$u", dto.Username);

            var count = Convert.ToInt32(check.ExecuteScalar());
            if (count > 0)
            {
                ModelState.AddModelError("", "Taki login już istnieje.");
                return View(dto);
            }
        }

        // Email unique (u Ciebie Email NOT NULL, ale brak UNIQUE w CREATE — zostawiamy walidację)
        using (var checkEmail = con.CreateCommand())
        {
            checkEmail.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($e));";
            checkEmail.Parameters.AddWithValue("$e", dto.Email);

            var count = Convert.ToInt32(checkEmail.ExecuteScalar());
            if (count > 0)
            {
                ModelState.AddModelError("", "Taki email już istnieje.");
                return View(dto);
            }
        }

        // Pesel unique? (w CREATE nie widać UNIQUE, ale zostawiamy)
        using (var checkPesel = con.CreateCommand())
        {
            checkPesel.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE TRIM(pesel) = TRIM($p);";
            checkPesel.Parameters.AddWithValue("$p", dto.Pesel);

            var count = Convert.ToInt32(checkPesel.ExecuteScalar());
            if (count > 0)
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

    // =========================
    // LOGOWANIE (bez Rola)
    // =========================
    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return BadRequest(new { ok = false, msg = "Brak loginu lub hasła" });

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { ok = false, msg = "Brak bazy", path = DbPath });

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT username
FROM Uzytkownicy
WHERE LOWER(TRIM(username)) = LOWER(TRIM($u))
  AND TRIM(COALESCE(Password,'')) = TRIM($p)
  AND COALESCE(czy_zapomniany,0) = 0
LIMIT 1;
";
        cmd.Parameters.AddWithValue("$u", username.Trim());
        cmd.Parameters.AddWithValue("$p", password.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Unauthorized(new { ok = false, msg = "Błędne dane" });

        return Json(new { ok = true, username = reader["username"]?.ToString() });
    }

    // =========================
    // API: lista userów (zostaje, ale zgodnie z nową bazą)
    // =========================
    [HttpGet]
    public IActionResult ApiUsers(string? login = null, string? name = null, string? pesel = null)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { error = "Brak pliku bazy", path = DbPath });

        var results = new List<object>();
        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT id, username, firstName, LastName, pesel, Email
FROM Uzytkownicy
WHERE COALESCE(czy_zapomniany,0) = 0
  AND ($login IS NULL OR LOWER(TRIM(username)) LIKE '%' || LOWER(TRIM($login)) || '%')
  AND ($name  IS NULL OR LOWER(TRIM(firstName || ' ' || LastName)) LIKE '%' || LOWER(TRIM($name)) || '%')
  AND ($pesel IS NULL OR TRIM(pesel) LIKE '%' || TRIM($pesel) || '%')
ORDER BY id;
";
        cmd.Parameters.AddWithValue("$login", (object?)login ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pesel", (object?)pesel ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new
            {
                id = reader["id"],
                username = reader["username"],
                firstName = reader["firstName"],
                lastName = reader["LastName"],
                email = reader["Email"],
                pesel = reader["pesel"]
            });
        }

        return Json(results);
    }

    // =========================
    // API: lista zapomnianych (RODO)
    // =========================
   public IActionResult ForgottenUsers(string fname, long? adminId)
{
    if (!System.IO.File.Exists(DbPath))
        return StatusCode(500, new { msg = "Brak bazy", path = DbPath });

    using var con = new SqliteConnection($"Data Source={DbPath}");
    con.Open();

    var users = new List<UserListRowDto>();

    using var cmd = con.CreateCommand();
    cmd.CommandText = @"
SELECT 
    id,
    firstName,
    lastName,
    DataZapomnienia,
    ZapomnialUserId
FROM Uzytkownicy
WHERE czy_zapomniany = 1
    AND (@fname IS NULL OR (firstName || ' ' || lastName) LIKE '%' || @fname || '%')
    AND (@adminId IS NULL OR ZapomnialUserId = @adminId)
ORDER BY DataZapomnienia DESC;
";

    cmd.Parameters.AddWithValue("@fname", string.IsNullOrWhiteSpace(fname) ? DBNull.Value : fname);
    cmd.Parameters.AddWithValue("@adminId", adminId.HasValue ? adminId.Value : DBNull.Value);

    using var rd = cmd.ExecuteReader();
    while (rd.Read())
    {
        users.Add(new UserListRowDto
        {
            Id = rd.GetInt64(0),
            FirstName = rd.GetString(1),
            LastName = rd.GetString(2),
            Status = rd.IsDBNull(3) ? "" : rd.GetString(3), // Data zapomnienia
            Rola = rd.IsDBNull(4) ? "" : rd.GetInt64(4).ToString() // Admin ID
        });
    }

    ViewBag.Fname = fname;
    ViewBag.AdminId = adminId;

    return View(users);
}


    // =========================
    // API: jeden user (do ewentualnych popupów)
    // =========================
    [HttpGet]
    public IActionResult ApiUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT id, username, Password, firstName, LastName, pesel, Status, Plec, DataUrodzenia,
       Email, NrTelefonu,
       Miejscowosc, KodPocztowy, numer_posesji, Ulica, NrLokalu,
       COALESCE(czy_zapomniany,0) AS czy_zapomniany,
       DataZapomnienia,
       ZapomnialUserId
FROM Uzytkownicy
WHERE id = $id
LIMIT 1;
";
        cmd.Parameters.AddWithValue("$id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        return Json(new
        {
            id = r["id"],
            username = r["username"],
            password = r["Password"],
            firstName = r["firstName"],
            lastName = r["LastName"],
            pesel = r["pesel"],
            status = r["Status"],
            plec = Convert.ToInt32(r["Plec"]),
            dataUrodzenia = r["DataUrodzenia"],
            email = r["Email"],
            nrTelefonu = r["NrTelefonu"],
            miejscowosc = r["Miejscowosc"],
            kodPocztowy = r["KodPocztowy"],
            nrPosesji = r["numer_posesji"],
            ulica = r["Ulica"],
            nrLokalu = r["NrLokalu"],
            zapomniany = Convert.ToInt32(r["czy_zapomniany"]) == 1,
            dataZapomnienia = r["DataZapomnienia"],
            zapomnialUserId = r["ZapomnialUserId"]
        });
    }

    // ============================================
    // EDYCJA USERA - osobna strona (bez roli w Uzytkownicy)
    // ============================================
    [HttpGet]
    public IActionResult EditUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

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
            Username = r["username"]?.ToString(),
            Password = r["Password"]?.ToString(),
            FirstName = r["firstName"]?.ToString(),
            LastName = r["LastName"]?.ToString(),
            Pesel = r["pesel"]?.ToString(),
            Status = r["Status"]?.ToString(),
            Plec = PlecToText(Convert.ToInt32(r["Plec"])),
            DataUrodzenia = r["DataUrodzenia"]?.ToString(),
            Email = r["Email"]?.ToString(),
            NrTelefonu = r["NrTelefonu"]?.ToString(),
            Miejscowosc = r["Miejscowosc"]?.ToString(),
            KodPocztowy = r["KodPocztowy"]?.ToString(),
            NrPosesji = r["numer_posesji"]?.ToString(),
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

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

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
    // ZAPOMNIJ z widoku szczegółów (bez JS)
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
    // RODO: zapomnienie usera (dopasowane do nowej bazy)
    // ============================================
    [HttpPost]
    public IActionResult ForgetUser(long id, long adminId)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

        static string RandDigits(int len)
        {
            var rng = Random.Shared;
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)('0' + rng.Next(0, 10));
            return new string(chars);
        }

        static string RandLetters(int len)
        {
            const string a = "abcdefghijklmnopqrstuvwxyz";
            var rng = Random.Shared;
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = a[rng.Next(a.Length)];
            return char.ToUpper(chars[0]) + new string(chars, 1, len - 1);
        }

        static string RandToken(int len)
        {
            const string a = "abcdefghijklmnopqrstuvwxyz0123456789_";
            var rng = Random.Shared;
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = a[rng.Next(a.Length)];
            return new string(chars);
        }

        var newFirst = RandLetters(6);
        var newLast = RandLetters(8);
        var newPesel = RandDigits(11);

        var year = Random.Shared.Next(1950, 2006);
        var month = Random.Shared.Next(1, 13);
        var day = Random.Shared.Next(1, DateTime.DaysInMonth(year, month) + 1);
        var newDob = new DateTime(year, month, day).ToString("yyyy-MM-dd");

        var newPlec = Random.Shared.Next(0, 2); // 0/1
        var newStatus = "Nieaktywny";

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

    Status = $Status,

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

        cmd.Parameters.AddWithValue("$Status", newStatus);

        cmd.Parameters.AddWithValue("$Username", newUsername);
        cmd.Parameters.AddWithValue("$Password", newPassword);
        cmd.Parameters.AddWithValue("$Email", newEmail);

        var rows = cmd.ExecuteNonQuery();
        if (rows == 0) return NotFound(new { msg = "Nie znaleziono użytkownika" });

        // usuń przypisane uprawnienia
        using (var del = con.CreateCommand())
        {
            del.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uid;";
            del.Parameters.AddWithValue("$uid", id);
            del.ExecuteNonQuery();
        }

        return Ok(new { ok = true });
    }

    // ============================================
    // NADAJ UPRAWNIENIA (popup -> POST)
    // ============================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRole(long id, string rola)
    {
        if (string.IsNullOrWhiteSpace(rola))
            return BadRequest(new { msg = "Brak roli" });

        if (!System.IO.File.Exists(DbPath))
            return StatusCode(500, new { msg = "Brak bazy", path = DbPath });

        using var con = new SqliteConnection($"Data Source={DbPath}");
        con.Open();

        long permId;
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($n) LIMIT 1;";
            cmd.Parameters.AddWithValue("$n", rola.Trim());
            var obj = cmd.ExecuteScalar();
            if (obj == null) return BadRequest(new { msg = "Nie ma takiego uprawnienia w tabeli Uprawnienia" });
            permId = Convert.ToInt64(obj);
        }

        // jedna rola na usera
        using (var del = con.CreateCommand())
        {
            del.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uid;";
            del.Parameters.AddWithValue("$uid", id);
            del.ExecuteNonQuery();
        }

        using (var ins = con.CreateCommand())
        {
            ins.CommandText = @"
INSERT INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id)
VALUES ($pid, $uid);
";
            ins.Parameters.AddWithValue("$pid", permId);
            ins.Parameters.AddWithValue("$uid", id);
            ins.ExecuteNonQuery();
        }

        return RedirectToAction(nameof(UserDetails), new { id });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    // ============================================
// UPRAWNIENIA -> lista użytkowników z daną rolą
// ============================================
[HttpGet]
public IActionResult UsersByRole(string rola)
{
    if (string.IsNullOrWhiteSpace(rola))
        return BadRequest(new { msg = "Brak parametru rola" });

    if (!System.IO.File.Exists(DbPath))
        return NotFound(new { msg = "Brak bazy", path = DbPath });

    rola = rola.Trim();

    using var con = new SqliteConnection($"Data Source={DbPath}");
    con.Open();

    // 1) znajdź Id roli
    long roleId;
    using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($n) LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", rola);
        var obj = cmd.ExecuteScalar();
        if (obj == null)
            return NotFound(new { msg = "Nie znaleziono roli w tabeli Uprawnienia", rola });

        roleId = Convert.ToInt64(obj);
    }

    // 2) pobierz użytkowników z tą rolą
    var results = new List<UserListRowDto>();
    using (var cmd = con.CreateCommand())
    {
        cmd.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.Email,
       u.pesel,
       u.Status,
       $rola AS Rola
FROM Uzytkownik_Uprawnienia uu
JOIN Uzytkownicy u ON u.id = uu.uzytkownik_id
WHERE uu.uprawnienie_id = $rid
  AND COALESCE(u.czy_zapomniany,0) = 0
ORDER BY u.LastName, u.firstName, u.username;
";
        cmd.Parameters.AddWithValue("$rid", roleId);
        cmd.Parameters.AddWithValue("$rola", rola);

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
    }

    ViewBag.Rola = rola;
    return View(results);
}
}