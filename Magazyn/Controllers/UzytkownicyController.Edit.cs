using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet("/Uzytkownicy/EditUser/{id:long}")]
    public IActionResult EditUser(long id)
    {
        if (id <= 0)
            return BadRequest(new { msg = "Nieprawidłowe id", id });

        _logger.LogInformation("[AdminAccess] '{User}' otworzył edycję użytkownika id={TargetId} IP={RemoteIp}",
            SL(User.Identity?.Name), id, HttpContext.Connection.RemoteIpAddress);

        if (!System.IO.File.Exists(DbPath))
            return NotFound(new { msg = "Brak bazy", path = DbPath });

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        command.CommandText = @"
SELECT id, username, firstName, LastName, pesel, Status, Plec, DataUrodzenia,
       Email, NrTelefonu,
       Miejscowosc, KodPocztowy, numer_posesji, Ulica, NrLokalu
FROM Uzytkownicy
WHERE id = $id
LIMIT 1;
";
        command.Parameters.AddWithValue("$id", id);

        using var dbReader = command.ExecuteReader();
        if (!dbReader.Read())
            return NotFound(new { msg = "Nie znaleziono użytkownika", id });

        DateOnly? dataUrodzenia = null;
        var dataUrodzeniaDb = dbReader["DataUrodzenia"]?.ToString();
        if (DateOnly.TryParse(dataUrodzeniaDb, out var d))
            dataUrodzenia = d;

        var viewModel = new UserVm
        {
            Id = Convert.ToInt64(dbReader["id"]),
            Username = dbReader["username"]?.ToString() ?? "",

            // WAŻNE: nie wypełniamy hasła z bazy (dla bezpieczeństwa i żeby nie nadpisywać)
            Password = "",

            FirstName = dbReader["firstName"]?.ToString() ?? "",
            LastName = dbReader["LastName"]?.ToString() ?? "",
            Pesel = dbReader["pesel"]?.ToString() ?? "",
            Status = StatusToText(dbReader["Status"]),
            Plec = PlecToText(dbReader["Plec"] == DBNull.Value ? 0 : Convert.ToInt32(dbReader["Plec"])),
            DataUrodzenia = dataUrodzenia,
            Email = dbReader["Email"]?.ToString() ?? "",
            NrTelefonu = dbReader["NrTelefonu"]?.ToString() ?? "",
            Miejscowosc = dbReader["Miejscowosc"]?.ToString() ?? "",
            KodPocztowy = dbReader["KodPocztowy"]?.ToString() ?? "",
            NrPosesji = dbReader["numer_posesji"]?.ToString() ?? "",
            Ulica = dbReader["Ulica"]?.ToString(),
            NrLokalu = dbReader["NrLokalu"]?.ToString()
        };

        return View(viewModel);
    }

    [HttpPost("/Uzytkownicy/EditUser")]
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

        viewModel.Username = (viewModel.Username ?? "").Trim();
        viewModel.Password = (viewModel.Password ?? "").Trim(); // może być puste => nie zmieniamy
        viewModel.FirstName = (viewModel.FirstName ?? "").Trim();
        viewModel.LastName = (viewModel.LastName ?? "").Trim();
        viewModel.Pesel = (viewModel.Pesel ?? "").Trim();
        viewModel.Status = (viewModel.Status ?? "").Trim();
        viewModel.Plec = (viewModel.Plec ?? "").Trim();
        viewModel.Email = (viewModel.Email ?? "").Trim();
        viewModel.NrTelefonu = (viewModel.NrTelefonu ?? "").Trim();
        viewModel.Miejscowosc = (viewModel.Miejscowosc ?? "").Trim();
        viewModel.KodPocztowy = (viewModel.KodPocztowy ?? "").Trim();
        viewModel.NrPosesji = (viewModel.NrPosesji ?? "").Trim();
        viewModel.Ulica = (viewModel.Ulica ?? "").Trim();
        viewModel.NrLokalu = (viewModel.NrLokalu ?? "").Trim();

        var dataUrodzeniaStr = viewModel.DataUrodzenia?.ToString("yyyy-MM-dd");

        using var connection = Db.OpenConnection(DbPath);
        using var command = connection.CreateCommand();

        // Jeśli hasło puste -> nie aktualizujemy Password
        var updatePasswordSql = string.IsNullOrWhiteSpace(viewModel.Password)
            ? ""
            : ", Password = $password";

        command.CommandText = $@"
UPDATE Uzytkownicy
SET username      = $username
    {updatePasswordSql},
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

        command.Parameters.AddWithValue("$id", viewModel.Id);
        command.Parameters.AddWithValue("$username", viewModel.Username);

        if (!string.IsNullOrWhiteSpace(viewModel.Password))
            command.Parameters.AddWithValue("$password", viewModel.Password);

        command.Parameters.AddWithValue("$firstName", viewModel.FirstName);
        command.Parameters.AddWithValue("$lastName", viewModel.LastName);
        command.Parameters.AddWithValue("$pesel", viewModel.Pesel);
        command.Parameters.AddWithValue("$status", StatusToInt(viewModel.Status));
        command.Parameters.AddWithValue("$plec", PlecToInt(viewModel.Plec));

        command.Parameters.AddWithValue("$dataUrodzenia",
            string.IsNullOrWhiteSpace(dataUrodzeniaStr) ? DBNull.Value : dataUrodzeniaStr);

        command.Parameters.AddWithValue("$email", viewModel.Email);
        command.Parameters.AddWithValue("$nrTelefonu", viewModel.NrTelefonu);
        command.Parameters.AddWithValue("$miejscowosc", viewModel.Miejscowosc);
        command.Parameters.AddWithValue("$kodPocztowy", viewModel.KodPocztowy);
        command.Parameters.AddWithValue("$nrPosesji", viewModel.NrPosesji);

        command.Parameters.AddWithValue("$ulica",
            string.IsNullOrWhiteSpace(viewModel.Ulica) ? DBNull.Value : viewModel.Ulica);

        command.Parameters.AddWithValue("$nrLokalu",
            string.IsNullOrWhiteSpace(viewModel.NrLokalu) ? DBNull.Value : viewModel.NrLokalu);

        try
        {
            var affectedRows = command.ExecuteNonQuery();
            if (affectedRows == 0)
            {
                ModelState.AddModelError("", "Nie znaleziono użytkownika.");
                return View(viewModel);
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            // Tu zobaczysz dokładną przyczynę (UNIQUE, NOT NULL itd.)
            _logger.LogError(ex, "Błąd SQLite przy zapisie edycji usera id={TargetId}", viewModel.Id);
            ModelState.AddModelError("", $"Błąd zapisu do bazy: {ex.SqliteErrorCode} - {ex.Message}");
            return View(viewModel);
        }

        return RedirectToAction(nameof(UserDetails), new { id = viewModel.Id });
    }
}