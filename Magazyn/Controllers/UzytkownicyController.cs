using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Magazyn.Data;
using Magazyn.Models;
using Magazyn.Models.Dtos;

namespace Magazyn.Controllers;

/// <summary>
/// Kontroler zarządzający użytkownikami systemu: rejestracja, edycja,
/// przeglądanie, oraz obsługa prawa do bycia zapomnianym (RODO).
/// </summary>
[Authorize]
public class UzytkownicyController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UzytkownicyController> _logger;

    public UzytkownicyController(IWebHostEnvironment env, ILogger<UzytkownicyController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>Pełna ścieżka do pliku bazy danych SQLite.</summary>
    private string DbPath => Db.GetDbPath(_env);

    /// <summary>Usuwa znaki nowej linii z wartości wejściowej, aby zapobiec fałszowaniu wpisów w logach.</summary>
    private static string SL(string? value) =>
        (value ?? "").Replace('\r', '_').Replace('\n', '_');

    /// <summary>
    /// Konwertuje liczbową wartość płci (0/1) na czytelny ciąg tekstowy.
    /// </summary>
    /// <param name="genderValue">Wartość z bazy: 1 = Mężczyzna, 0 = Kobieta.</param>
    /// <returns>"Mężczyzna" lub "Kobieta".</returns>
    private static string PlecToText(int genderValue) => genderValue == 1 ? "Mężczyzna" : "Kobieta";

    /// <summary>
    /// Konwertuje tekstową nazwę płci na wartość liczbową do zapisu w bazie.
    /// </summary>
    /// <param name="genderText">Tekst: "Mężczyzna" lub dowolny inny (→ 0).</param>
    /// <returns>1 dla "Mężczyzna", 0 w pozostałych przypadkach.</returns>
    private static int PlecToInt(string? genderText)
    {
        if (string.IsNullOrWhiteSpace(genderText)) return 0;
        return genderText.Trim().Equals("Mężczyzna", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    /// <summary>
    /// Konwertuje wartość kolumny Status z bazy danych (INT lub DBNull)
    /// na czytelny ciąg tekstowy.
    /// </summary>
    /// <param name="dbValue">Wartość z kolumny Status (może być DBNull).</param>
    /// <returns>"Aktywny" gdy Status = 1, w przeciwnym razie "Nieaktywny".</returns>
    private static string StatusToText(object dbValue)
    {
        if (dbValue == DBNull.Value) return "Nieaktywny";
        return Convert.ToInt32(dbValue) == 1 ? "Aktywny" : "Nieaktywny";
    }

    /// <summary>
    /// Konwertuje tekstową nazwę statusu na wartość liczbową do zapisu w bazie.
    /// Domyślnie zwraca 1 (Aktywny) gdy tekst jest pusty.
    /// </summary>
    /// <param name="statusText">Tekst: "Aktywny" lub dowolny inny (→ 0).</param>
    /// <returns>1 dla "Aktywny", 0 w pozostałych przypadkach.</returns>
    private static int StatusToInt(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText)) return 1; // domyślnie aktywny
        return statusText.Trim().Equals("Aktywny", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    // ============================================
    // ADMIN PANEL
    // ============================================

    /// <summary>
    /// Wyświetla panel administracyjny z listą aktywnych użytkowników.
    /// Obsługuje filtrowanie po loginie, nazwisku i PESEL.
    /// Dołącza do każdego użytkownika jego role z tabeli Uprawnienia.
    /// </summary>
    /// <param name="login">Opcjonalny filtr na login.</param>
    /// <param name="name">Opcjonalny filtr na imię i nazwisko.</param>
    /// <param name="pesel">Opcjonalny filtr na PESEL.</param>
    [HttpGet]
    public IActionResult AdminPanel(string? login = null, string? name = null, string? pesel = null)
    {
        _logger.LogInformation("[AdminAccess] '{User}' otworzył AdminPanel [login={Login}, name={Name}, pesel={Pesel}] IP={RemoteIp}",
            SL(User.Identity?.Name), SL(login), SL(name), SL(pesel), HttpContext.Connection.RemoteIpAddress);

        ViewBag.Login = login ?? "";
        ViewBag.Name  = name  ?? "";
        ViewBag.Pesel = pesel ?? "";

        var userList = new List<UserListRowDto>();
        if (!System.IO.File.Exists(DbPath))
            return View(userList);

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Pobiera listę użytkowników z ich rolami (GROUP_CONCAT dla wielu ról).
        // Filtry na login/imię/pesel są opcjonalne – pusty parametr wyłącza filtrowanie.
        // Wykluczamy konta oznaczone jako zapomniane (RODO).
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
        command.Parameters.AddWithValue("$name",  name  ?? "");
        command.Parameters.AddWithValue("$pesel", pesel ?? "");

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            userList.Add(new UserListRowDto
            {
                Id        = Convert.ToInt64(dbReader["id"]),
                Username  = dbReader["username"]?.ToString(),
                FirstName = dbReader["firstName"]?.ToString(),
                LastName  = dbReader["LastName"]?.ToString(),
                Email     = dbReader["Email"]?.ToString(),
                Pesel     = dbReader["pesel"]?.ToString(),
                Status    = dbReader["Status"]?.ToString(),
                Rola      = dbReader["Rola"]?.ToString()
            });
        }

        return View(userList);
    }

    // ============================================
    // SZCZEGÓŁY USERA
    // ============================================

    /// <summary>
    /// Wyświetla stronę ze szczegółowymi danymi użytkownika,
    /// łącznie z adresem, danymi kontaktowymi i przypisanymi rolami.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika.</param>
    [HttpGet]
    public IActionResult UserDetails(long id)
    {
        _logger.LogInformation("[AdminAccess] '{User}' przejrzał szczegóły użytkownika id={TargetId} IP={RemoteIp}",
            SL(User.Identity?.Name), id, HttpContext.Connection.RemoteIpAddress);

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Pobiera pełne dane użytkownika wraz z jego rolami (JOIN przez tabelę pośrednią).
        // GROUP BY wymagany przez GROUP_CONCAT; LIMIT 1 dla pewności.
        command.CommandText = @"
SELECT u.id,
       u.username,
       u.firstName,
       u.LastName,
       u.pesel,
       CASE WHEN u.Status = 1 THEN 'Aktywny' ELSE 'Nieaktywny' END AS Status,
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
        command.Parameters.AddWithValue("$id", id);

        using var dbReader = command.ExecuteReader();
        if (!dbReader.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        var userDetails = new UserDetailsDto
        {
            Id           = Convert.ToInt64(dbReader["id"]),
            Username     = dbReader["username"]?.ToString(),
            FirstName    = dbReader["firstName"]?.ToString(),
            LastName     = dbReader["LastName"]?.ToString(),
            Pesel        = dbReader["pesel"]?.ToString(),
            Status       = dbReader["Status"]?.ToString(),
            Plec         = dbReader["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(dbReader["Plec"]),
            Rola         = dbReader["Rola"]?.ToString(),
            DataUrodzenia = dbReader["DataUrodzenia"]?.ToString(),
            NrTelefonu   = dbReader["NrTelefonu"]?.ToString(),
            Miejscowosc  = dbReader["Miejscowosc"]?.ToString(),
            KodPocztowy  = dbReader["KodPocztowy"]?.ToString(),
            Ulica        = dbReader["Ulica"]?.ToString(),
            NrPosesji    = dbReader["numer_posesji"]?.ToString(),
            NrLokalu     = dbReader["NrLokalu"]?.ToString(),
        };

        return View(userDetails);
    }

    // ============================================
    // REJESTRACJA
    // ============================================
    [HttpGet]
    public IActionResult Rejestracja() => View();

    /// <summary>
    /// Rejestruje nowego użytkownika w bazie danych.
    /// Przed zapisem weryfikuje unikalność loginu, adresu e-mail i numeru PESEL.
    /// </summary>
    /// <param name="dto">Dane rejestracyjne przesłane z formularza.</param>
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

        _logger.LogInformation("[AdminAccess] '{User}' rejestruje nowego użytkownika login='{NewLogin}' IP={RemoteIp}",
            SL(User.Identity?.Name), SL(dto.Username), HttpContext.Connection.RemoteIpAddress);

        // Przycinamy białe znaki ze wszystkich pól tekstowych przed zapisem do bazy
        dto.Username     = (dto.Username     ?? "").Trim();
        dto.Password     = (dto.Password     ?? "").Trim();
        dto.FirstName    = (dto.FirstName    ?? "").Trim();
        dto.LastName     = (dto.LastName     ?? "").Trim();
        dto.Pesel        = (dto.Pesel        ?? "").Trim();
        dto.Status       = (dto.Status       ?? "").Trim();
        dto.Plec         = (dto.Plec         ?? "").Trim();
        dto.DataUrodzenia = (dto.DataUrodzenia ?? "").Trim();
        dto.Email        = (dto.Email        ?? "").Trim();
        dto.NrTelefonu   = (dto.NrTelefonu   ?? "").Trim();
        dto.Miejscowosc  = (dto.Miejscowosc  ?? "").Trim();
        dto.KodPocztowy  = (dto.KodPocztowy  ?? "").Trim();
        dto.NrPosesji    = (dto.NrPosesji    ?? "").Trim();
        dto.Ulica        = (dto.Ulica        ?? "").Trim();
        dto.NrLokalu     = (dto.NrLokalu     ?? "").Trim();

        using var connection = Db.OpenConnection(DbPath);

        // Sprawdzenie unikalności loginu (case-insensitive)
        using (var checkUsernameCommand = connection.CreateCommand())
        {
            checkUsernameCommand.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(username)) = LOWER(TRIM($username));";
            checkUsernameCommand.Parameters.AddWithValue("$username", dto.Username);
            if (Convert.ToInt32(checkUsernameCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("", "Taki login już istnieje.");
                return View(dto);
            }
        }

        // Sprawdzenie unikalności adresu e-mail (case-insensitive)
        using (var checkEmailCommand = connection.CreateCommand())
        {
            checkEmailCommand.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE LOWER(TRIM(Email)) = LOWER(TRIM($email));";
            checkEmailCommand.Parameters.AddWithValue("$email", dto.Email);
            if (Convert.ToInt32(checkEmailCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("", "Taki email już istnieje.");
                return View(dto);
            }
        }

        // Sprawdzenie unikalności numeru PESEL
        using (var checkPeselCommand = connection.CreateCommand())
        {
            checkPeselCommand.CommandText = @"SELECT COUNT(*) FROM Uzytkownicy WHERE TRIM(pesel) = TRIM($pesel);";
            checkPeselCommand.Parameters.AddWithValue("$pesel", dto.Pesel);
            if (Convert.ToInt32(checkPeselCommand.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError("", "Taki PESEL już istnieje.");
                return View(dto);
            }
        }

        // Wstawienie nowego użytkownika do bazy danych.
        // Pola opcjonalne (Ulica, NrLokalu) są zapisywane jako NULL gdy są puste.
        // blokada_do, czy_zapomniany, DataZapomnienia, ZapomnialUserId, liczba_blednych_logowan
        // inicjalizowane do wartości domyślnych (NULL / 0).
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
     0, $nrPosesji, NULL, $status);
";
            insertCommand.Parameters.AddWithValue("$email",        dto.Email);
            insertCommand.Parameters.AddWithValue("$firstName",    dto.FirstName);
            insertCommand.Parameters.AddWithValue("$username",     dto.Username);
            insertCommand.Parameters.AddWithValue("$miejscowosc",  dto.Miejscowosc);
            insertCommand.Parameters.AddWithValue("$lastName",     dto.LastName);
            insertCommand.Parameters.AddWithValue("$nrLokalu",     string.IsNullOrWhiteSpace(dto.NrLokalu) ? DBNull.Value : dto.NrLokalu);
            insertCommand.Parameters.AddWithValue("$pesel",        dto.Pesel);
            insertCommand.Parameters.AddWithValue("$plec",         PlecToInt(dto.Plec));
            insertCommand.Parameters.AddWithValue("$nrTelefonu",   dto.NrTelefonu);
            insertCommand.Parameters.AddWithValue("$ulica",        string.IsNullOrWhiteSpace(dto.Ulica) ? DBNull.Value : dto.Ulica);
            insertCommand.Parameters.AddWithValue("$dataUrodzenia", dto.DataUrodzenia);
            insertCommand.Parameters.AddWithValue("$password",     string.IsNullOrWhiteSpace(dto.Password) ? DBNull.Value : dto.Password);
            insertCommand.Parameters.AddWithValue("$kodPocztowy",  dto.KodPocztowy);
            insertCommand.Parameters.AddWithValue("$nrPosesji",    dto.NrPosesji);
            insertCommand.Parameters.AddWithValue("$status",       StatusToInt(dto.Status));
            insertCommand.ExecuteNonQuery();
        }

        // Przypisanie roli (uprawnienia) nowemu użytkownikowi
        if (!string.IsNullOrWhiteSpace(dto.Rola))
        {
            // Pobierz ID nowo dodanego użytkownika
            long newUserId;
            using (var lastIdCommand = connection.CreateCommand())
            {
                lastIdCommand.CommandText = "SELECT last_insert_rowid();";
                newUserId = Convert.ToInt64(lastIdCommand.ExecuteScalar());
            }

            // Znajdź ID uprawnienia po nazwie
            using (var roleIdCommand = connection.CreateCommand())
            {
                roleIdCommand.CommandText = @"SELECT Id FROM Uprawnienia WHERE TRIM(Nazwa) = TRIM($nazwaRoli) LIMIT 1;";
                roleIdCommand.Parameters.AddWithValue("$nazwaRoli", dto.Rola.Trim());
                var roleIdScalar = roleIdCommand.ExecuteScalar();
                if (roleIdScalar != null)
                {
                    var roleId = Convert.ToInt64(roleIdScalar);
                    using var insertRoleCommand = connection.CreateCommand();
                    insertRoleCommand.CommandText = @"
INSERT OR IGNORE INTO Uzytkownik_Uprawnienia (uprawnienie_id, uzytkownik_id)
VALUES ($roleId, $userId);
";
                    insertRoleCommand.Parameters.AddWithValue("$roleId", roleId);
                    insertRoleCommand.Parameters.AddWithValue("$userId", newUserId);
                    insertRoleCommand.ExecuteNonQuery();
                }
            }
        }

        return RedirectToAction(nameof(AdminPanel));
    }

    // ============================================
    // EDYCJA USERA
    // ============================================

    /// <summary>
    /// Wyświetla formularz edycji danych użytkownika wstępnie wypełniony
    /// aktualną zawartością rekordu z bazy danych.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika.</param>
    [HttpGet]
    public IActionResult EditUser(long id)
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Pobiera dane użytkownika do wstępnego wypełnienia formularza edycji.
        command.CommandText = @"
SELECT id, username, Password, firstName, LastName, pesel, Status, Plec, DataUrodzenia,
       Email, NrTelefonu,
       Miejscowosc, KodPocztowy, numer_posesji, Ulica, NrLokalu
FROM Uzytkownicy
WHERE id = $id
LIMIT 1;
";
        command.Parameters.AddWithValue("$id", id);

        using var dbReader = command.ExecuteReader();
        if (!dbReader.Read()) return NotFound(new { msg = "Nie znaleziono użytkownika" });

        var viewModel = new UserVm
        {
            Id           = Convert.ToInt64(dbReader["id"]),
            Username     = dbReader["username"]?.ToString()    ?? "",
            Password     = dbReader["Password"]?.ToString()    ?? "",
            FirstName    = dbReader["firstName"]?.ToString()   ?? "",
            LastName     = dbReader["LastName"]?.ToString()    ?? "",
            Pesel        = dbReader["pesel"]?.ToString()       ?? "",
            Status       = StatusToText(dbReader["Status"]),
            Plec         = PlecToText(dbReader["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(dbReader["Plec"])),
            DataUrodzenia = dbReader["DataUrodzenia"]?.ToString() ?? "",
            Email        = dbReader["Email"]?.ToString()       ?? "",
            NrTelefonu   = dbReader["NrTelefonu"]?.ToString()  ?? "",
            Miejscowosc  = dbReader["Miejscowosc"]?.ToString() ?? "",
            KodPocztowy  = dbReader["KodPocztowy"]?.ToString() ?? "",
            NrPosesji    = dbReader["numer_posesji"]?.ToString() ?? "",
            Ulica        = dbReader["Ulica"]?.ToString(),
            NrLokalu     = dbReader["NrLokalu"]?.ToString()
        };

        return View(viewModel);
    }

    /// <summary>
    /// Zapisuje zmodyfikowane dane użytkownika do bazy danych.
    /// Pola opcjonalne (Ulica, NrLokalu, Password) są zapisywane jako NULL gdy są puste.
    /// </summary>
    /// <param name="viewModel">Dane z formularza edycji.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditUser(UserVm viewModel)
    {
        if (!ModelState.IsValid)
            return View(viewModel);

        if (!System.IO.File.Exists(DbPath))
        {
            ModelState.AddModelError("", $"Nie znaleziono bazy danych: {DbPath}");
            return View(viewModel);
        }

        _logger.LogInformation("[AdminAccess] '{User}' edytuje użytkownika id={TargetId} IP={RemoteIp}",
            SL(User.Identity?.Name), viewModel.Id, HttpContext.Connection.RemoteIpAddress);
        viewModel.Username     = (viewModel.Username     ?? "").Trim();
        viewModel.Password     = (viewModel.Password     ?? "").Trim();
        viewModel.FirstName    = (viewModel.FirstName    ?? "").Trim();
        viewModel.LastName     = (viewModel.LastName     ?? "").Trim();
        viewModel.Pesel        = (viewModel.Pesel        ?? "").Trim();
        viewModel.Status       = (viewModel.Status       ?? "").Trim();
        viewModel.Plec         = (viewModel.Plec         ?? "").Trim();
        viewModel.DataUrodzenia = (viewModel.DataUrodzenia ?? "").Trim();
        viewModel.Email        = (viewModel.Email        ?? "").Trim();
        viewModel.NrTelefonu   = (viewModel.NrTelefonu   ?? "").Trim();
        viewModel.Miejscowosc  = (viewModel.Miejscowosc  ?? "").Trim();
        viewModel.KodPocztowy  = (viewModel.KodPocztowy  ?? "").Trim();
        viewModel.NrPosesji    = (viewModel.NrPosesji    ?? "").Trim();
        viewModel.Ulica        = (viewModel.Ulica        ?? "").Trim();
        viewModel.NrLokalu     = (viewModel.NrLokalu     ?? "").Trim();

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Aktualizuje wszystkie pola edytowalne użytkownika.
        // Puste pola opcjonalne (Password, Ulica, NrLokalu) zapisywane jako NULL.
        command.CommandText = @"
UPDATE Uzytkownicy
SET username      = $username,
    Password      = $password,
    firstName     = $firstName,
    LastName      = $lastName,
    pesel         = $pesel,
    Status        = $status,
    Plec          = $plec,
    DataUrodzenia = $dataUrodzenia,
    Email         = $email,
    NrTelefonu    = $nrTelefonu,
    Miejscowosc   = $miejscowosc,
    KodPocztowy   = $kodPocztowy,
    numer_posesji = $nrPosesji,
    Ulica         = $ulica,
    NrLokalu      = $nrLokalu
WHERE id = $id;
";
        command.Parameters.AddWithValue("$id",           viewModel.Id);
        command.Parameters.AddWithValue("$username",     viewModel.Username);
        command.Parameters.AddWithValue("$password",     string.IsNullOrWhiteSpace(viewModel.Password) ? DBNull.Value : viewModel.Password);
        command.Parameters.AddWithValue("$firstName",    viewModel.FirstName);
        command.Parameters.AddWithValue("$lastName",     viewModel.LastName);
        command.Parameters.AddWithValue("$pesel",        viewModel.Pesel);
        command.Parameters.AddWithValue("$status",       StatusToInt(viewModel.Status));
        command.Parameters.AddWithValue("$plec",         PlecToInt(viewModel.Plec));
        command.Parameters.AddWithValue("$dataUrodzenia", viewModel.DataUrodzenia);
        command.Parameters.AddWithValue("$email",        viewModel.Email);
        command.Parameters.AddWithValue("$nrTelefonu",   viewModel.NrTelefonu);
        command.Parameters.AddWithValue("$miejscowosc",  viewModel.Miejscowosc);
        command.Parameters.AddWithValue("$kodPocztowy",  viewModel.KodPocztowy);
        command.Parameters.AddWithValue("$nrPosesji",    viewModel.NrPosesji);
        command.Parameters.AddWithValue("$ulica",        string.IsNullOrWhiteSpace(viewModel.Ulica) ? DBNull.Value : viewModel.Ulica);
        command.Parameters.AddWithValue("$nrLokalu",     string.IsNullOrWhiteSpace(viewModel.NrLokalu) ? DBNull.Value : viewModel.NrLokalu);

        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows == 0)
        {
            ModelState.AddModelError("", "Nie znaleziono użytkownika.");
            return View(viewModel);
        }

        return RedirectToAction(nameof(UserDetails), new { id = viewModel.Id });
    }

    // ============================================
    // FORGOTTEN USERS (RODO)
    // ============================================

    /// <summary>
    /// Wyświetla listę użytkowników, którzy zostali zapomniani (usunięci zgodnie z RODO).
    /// Obsługuje filtrowanie po nazwisku i identyfikatorze administratora, który wykonał operację.
    /// </summary>
    /// <param name="fname">Opcjonalny filtr na imię i nazwisko (po zanonimizowaniu).</param>
    /// <param name="adminId">Opcjonalny filtr na ID administratora, który wykonał zapomnienie.</param>
    [HttpGet]
    public IActionResult ForgottenUsers(string? fname = null, long? adminId = null)
    {
        ViewBag.Fname   = fname   ?? "";
        ViewBag.AdminId = adminId?.ToString() ?? "";

        var forgottenList = new List<ForgottenRowDto>();

        if (!System.IO.File.Exists(DbPath))
            return View(forgottenList);

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Pobiera zapomniane konta z opcjonalnym filtrowaniem.
        // Wyniki sortowane od najnowszego do najstarszego.
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
        command.Parameters.AddWithValue("$fname",   string.IsNullOrWhiteSpace(fname) ? "" : fname.Trim());
        command.Parameters.AddWithValue("$adminId", adminId.HasValue ? adminId.Value : DBNull.Value);

        using var dbReader = command.ExecuteReader();
        while (dbReader.Read())
        {
            var firstName = dbReader.IsDBNull(1) ? "" : dbReader.GetString(1);
            var lastName  = dbReader.IsDBNull(2) ? "" : dbReader.GetString(2);
            forgottenList.Add(new ForgottenRowDto
            {
                Id                  = dbReader.GetInt64(0),
                FullNameAfterForget  = $"{firstName} {lastName}".Trim(),
                DataZapomnienia     = dbReader.IsDBNull(3) ? "" : dbReader.GetString(3),
                ZapomnialUserId     = dbReader.IsDBNull(4) ? "" : dbReader.GetInt64(4).ToString()
            });
        }

        return View(forgottenList);
    }

    // ============================================
    // ZAPOMNIJ z widoku szczegółów
    // ============================================

    /// <summary>
    /// Wrapper wywoływany z widoku szczegółów użytkownika – po pomyślnym zapomnieniu
    /// przekierowuje do panelu administratora.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika do zapomnienia.</param>
    /// <param name="adminId">Identyfikator administratora wykonującego operację.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgetUserFromDetails(long id, long adminId)
    {
        var actionResult = ForgetUser(id, adminId);
        if (actionResult is OkObjectResult)
            return RedirectToAction(nameof(AdminPanel));
        return actionResult;
    }

    // ============================================
    // RODO: zapomnienie usera
    // ============================================

    /// <summary>
    /// Anonimizuje dane osobowe użytkownika zgodnie z wymogami RODO.
    /// Zastępuje wszystkie dane osobowe losowymi wartościami, oznacza konto
    /// jako zapomniane i usuwa przypisane role.
    /// Używa <see cref="RandomNumberGenerator"/> zamiast <see cref="Random"/>
    /// dla kryptograficznie bezpiecznej losowości.
    /// </summary>
    /// <param name="id">Identyfikator użytkownika do anonimizacji.</param>
    /// <param name="adminId">Identyfikator administratora wykonującego operację (zapisywany w ZapomnialUserId).</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgetUser(long id, long adminId)
    {
        _logger.LogWarning("[AdminAccess] '{User}' wykonuje RODO-zapomnienie użytkownika id={TargetId} (adminId={AdminId}) IP={RemoteIp}",
            SL(User.Identity?.Name), id, adminId, HttpContext.Connection.RemoteIpAddress);

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);

        // Generuje losową liczbę z zakresu [0, maxExclusive) przy użyciu kryptograficznego RNG
        static int SecureRandomInt(int maxExclusive) =>
            RandomNumberGenerator.GetInt32(maxExclusive);

        // Generuje ciąg losowych cyfr o zadanej długości
        static string RandomDigits(int length)
        {
            var charBuffer = new char[length];
            for (int index = 0; index < length; index++)
                charBuffer[index] = (char)('0' + SecureRandomInt(10));
            return new string(charBuffer);
        }

        // Generuje ciąg losowych liter (pierwsza wielka, reszta małe)
        static string RandomLetters(int length)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz";
            var charBuffer = new char[length];
            for (int index = 0; index < length; index++)
                charBuffer[index] = alphabet[SecureRandomInt(alphabet.Length)];
            return char.ToUpper(charBuffer[0]) + new string(charBuffer, 1, length - 1);
        }

        // Generuje losowy token złożony z małych liter, cyfr i znaku podkreślenia
        static string RandomToken(int length)
        {
            const string tokenAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789_";
            var charBuffer = new char[length];
            for (int index = 0; index < length; index++)
                charBuffer[index] = tokenAlphabet[SecureRandomInt(tokenAlphabet.Length)];
            return new string(charBuffer);
        }

        // Generowanie losowych danych zastępczych
        var anonymizedFirstName = RandomLetters(6);
        var anonymizedLastName  = RandomLetters(8);
        var anonymizedPesel     = RandomDigits(11);

        var birthYear  = 1950 + SecureRandomInt(56); // zakres 1950–2005
        var birthMonth = 1 + SecureRandomInt(12);
        var birthDay   = 1 + SecureRandomInt(DateTime.DaysInMonth(birthYear, birthMonth));
        var anonymizedBirthDate = new DateTime(birthYear, birthMonth, birthDay).ToString("yyyy-MM-dd");

        var anonymizedGender   = SecureRandomInt(2);
        var anonymizedUsername = "del_" + RandomToken(10);
        var anonymizedPassword = RandomToken(12);
        var anonymizedEmail    = $"{RandomToken(8)}@example.com";

        using var command = connection.CreateCommand();

        // Nadpisuje dane osobowe użytkownika losowymi wartościami,
        // oznacza konto jako zapomniane i dezaktywuje je.
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
        command.Parameters.AddWithValue("$id",           id);
        command.Parameters.AddWithValue("$adminId",      adminId);
        command.Parameters.AddWithValue("$firstName",    anonymizedFirstName);
        command.Parameters.AddWithValue("$lastName",     anonymizedLastName);
        command.Parameters.AddWithValue("$pesel",        anonymizedPesel);
        command.Parameters.AddWithValue("$dataUrodzenia", anonymizedBirthDate);
        command.Parameters.AddWithValue("$plec",         anonymizedGender);
        command.Parameters.AddWithValue("$username",     anonymizedUsername);
        command.Parameters.AddWithValue("$password",     anonymizedPassword);
        command.Parameters.AddWithValue("$email",        anonymizedEmail);

        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows == 0)
            return NotFound(new { msg = "Nie znaleziono użytkownika" });

        // Usuwa wszystkie role zapomnianego użytkownika
        using (var deletePermissionsCommand = connection.CreateCommand())
        {
            deletePermissionsCommand.CommandText = @"DELETE FROM Uzytkownik_Uprawnienia WHERE uzytkownik_id = $uzytkownikId;";
            deletePermissionsCommand.Parameters.AddWithValue("$uzytkownikId", id);
            deletePermissionsCommand.ExecuteNonQuery();
        }

        return Ok(new { ok = true });
    }
}