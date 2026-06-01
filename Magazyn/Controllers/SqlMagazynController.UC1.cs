using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class MagazynController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu,Pracownik magazynu")]
    public IActionResult RejestracjaTowaru()
    {
        if (!System.IO.File.Exists(DbPath)) return View(new RejestracjaTowaruVm());
        
        using var connection = Db.OpenConnection(DbPath);
        var viewModel = new RejestracjaTowaruVm
        {
            Rodzaje = GetRodzaje(connection),
            JednostkiMiary = GetJednostkiMiary(connection),
            StawkiVat = GetStawkiVat(connection)
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu,Pracownik magazynu")]
    public IActionResult RejestracjaTowaru(RejestracjaTowaruVm viewModel)
    {
        using var connection = Db.OpenConnection(DbPath);

        if (!ModelState.IsValid)
        {
            ReloadViewModelLists(viewModel, connection);
            return View(viewModel);
        }

        string nazwaTrimmed = viewModel.NazwaTowaru?.Trim() ?? string.Empty;

        using (var findCommand = connection.CreateCommand())
        {
            findCommand.CommandText = "SELECT COUNT(1) FROM Towary WHERE LOWER(TRIM(NazwaTowaru)) = LOWER(TRIM($nazwa))";
            findCommand.Parameters.AddWithValue("$nazwa", nazwaTrimmed);
            
            var count = Convert.ToInt64(findCommand.ExecuteScalar());
            if (count > 0)
            {
                ModelState.AddModelError("NazwaTowaru", "Towar o podanej nazwie już istnieje.");
                ReloadViewModelLists(viewModel, connection);
                return View(viewModel);
            }
        }

        var userId = GetCurrentUserId();
        long towarId;

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = "INSERT INTO Towary (NazwaTowaru, RodzajId, JednostkaMiaryId, AktualnaIlosc) VALUES ($nazwa, $rodzajId, $jmId, $ilosc)";
            insertCommand.Parameters.AddWithValue("$nazwa", nazwaTrimmed);
            insertCommand.Parameters.AddWithValue("$rodzajId", viewModel.RodzajId);
            insertCommand.Parameters.AddWithValue("$jmId", viewModel.JednostkaMiaryId);
            insertCommand.Parameters.AddWithValue("$ilosc", (double)viewModel.Ilosc!.Value); 
            insertCommand.ExecuteNonQuery();

            using var lastIdCommand = connection.CreateCommand();
            lastIdCommand.CommandText = "SELECT last_insert_rowid()";
            towarId = Convert.ToInt64(lastIdCommand.ExecuteScalar());
        }

        using (var registrationCommand = connection.CreateCommand())
        {
            registrationCommand.CommandText = @"
                INSERT INTO RejestracjeTowaru 
                (TowarId, Ilosc, CenaNetto, StawkaVatId, Opis, Dostawca, DataDostawy, DataRejestracji, RejestrujacyUserId) 
                VALUES 
                ($towarId, $ilosc, $cena, $vatId, $opis, $dostawca, $dataDostawy, datetime('now'), $userId)";
            
            registrationCommand.Parameters.AddWithValue("$towarId", towarId);
            registrationCommand.Parameters.AddWithValue("$ilosc", (double)viewModel.Ilosc!.Value);
            registrationCommand.Parameters.AddWithValue("$cena", (double)viewModel.CenaNetto!.Value);
            registrationCommand.Parameters.AddWithValue("$vatId", viewModel.StawkaVatId);
            registrationCommand.Parameters.AddWithValue("$opis", string.IsNullOrWhiteSpace(viewModel.Opis) ? DBNull.Value : (object)viewModel.Opis);
            registrationCommand.Parameters.AddWithValue("$dostawca", string.IsNullOrWhiteSpace(viewModel.Dostawca) ? DBNull.Value : (object)viewModel.Dostawca);
            registrationCommand.Parameters.AddWithValue("$dataDostawy", string.IsNullOrWhiteSpace(viewModel.DataDostawy) ? DBNull.Value : (object)viewModel.DataDostawy);
            registrationCommand.Parameters.AddWithValue("$userId", userId);
            registrationCommand.ExecuteNonQuery();
        }

        _logger.LogInformation("[MAG-UC1] '{User}' zarejestrował towar '{Towar}'", SL(User.Identity?.Name), SL(nazwaTrimmed));
        TempData["SuccessMessage"] = "Towar został poprawnie zarejestrowany";
        
        return RedirectToAction(nameof(StanyMagazynowe));
    }

    // Metoda pomocnicza, żeby nie powtarzać ładowania list
    private void ReloadViewModelLists(RejestracjaTowaruVm viewModel, System.Data.Common.DbConnection connection)
    {
        viewModel.Rodzaje = GetRodzaje(connection);
        viewModel.JednostkiMiary = GetJednostkiMiary(connection);
        viewModel.StawkiVat = GetStawkiVat(connection);
    }
}