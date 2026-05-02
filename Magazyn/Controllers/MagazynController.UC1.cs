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
        using var conn = Db.OpenConnection(DbPath);
        var vm = new RejestracjaTowaruVm
        {
            Rodzaje = GetRodzaje(conn),
            JednostkiMiary = GetJednostkiMiary(conn),
            StawkiVat = GetStawkiVat(conn)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu,Pracownik magazynu")]
    public IActionResult RejestracjaTowaru(RejestracjaTowaruVm vm)
    {
        using var conn = Db.OpenConnection(DbPath);

        if (!ModelState.IsValid)
        {
            vm.Rodzaje = GetRodzaje(conn);
            vm.JednostkiMiary = GetJednostkiMiary(conn);
            vm.StawkiVat = GetStawkiVat(conn);
            return View(vm);
        }

        var userId = GetCurrentUserId();

        // Find or create Towar
        long towarId;
        using (var findCmd = conn.CreateCommand())
        {
            findCmd.CommandText = "SELECT Id FROM Towary WHERE LOWER(TRIM(NazwaTowaru)) = LOWER(TRIM($nazwa)) LIMIT 1";
            findCmd.Parameters.AddWithValue("$nazwa", vm.NazwaTowaru.Trim());
            var existing = findCmd.ExecuteScalar();
            if (existing != null && existing is not DBNull)
            {
                towarId = Convert.ToInt64(existing);
                using var updCmd = conn.CreateCommand();
                updCmd.CommandText = "UPDATE Towary SET AktualnaIlosc = AktualnaIlosc + $ilosc WHERE Id = $id";
                updCmd.Parameters.AddWithValue("$ilosc", (double)vm.Ilosc);
                updCmd.Parameters.AddWithValue("$id", towarId);
                updCmd.ExecuteNonQuery();
            }
            else
            {
                using var insCmd = conn.CreateCommand();
                insCmd.CommandText = "INSERT INTO Towary (NazwaTowaru, RodzajId, JednostkaMiaryId, AktualnaIlosc) VALUES ($nazwa, $rodzajId, $jmId, $ilosc)";
                insCmd.Parameters.AddWithValue("$nazwa", vm.NazwaTowaru.Trim());
                insCmd.Parameters.AddWithValue("$rodzajId", vm.RodzajId);
                insCmd.Parameters.AddWithValue("$jmId", vm.JednostkaMiaryId);
                insCmd.Parameters.AddWithValue("$ilosc", (double)vm.Ilosc);
                insCmd.ExecuteNonQuery();

                using var lastIdCmd = conn.CreateCommand();
                lastIdCmd.CommandText = "SELECT last_insert_rowid()";
                towarId = Convert.ToInt64(lastIdCmd.ExecuteScalar());
            }
        }

        // Create registration record
        using (var regCmd = conn.CreateCommand())
        {
            regCmd.CommandText = @"
                INSERT INTO RejestracjeTowaru
                    (TowarId, Ilosc, CenaNetto, StawkaVatId, Opis, Dostawca, DataDostawy, DataRejestracji, RejestrujacyUserId)
                VALUES
                    ($towarId, $ilosc, $cena, $vatId, $opis, $dostawca, $dataDostawy, datetime('now'), $userId)";
            regCmd.Parameters.AddWithValue("$towarId", towarId);
            regCmd.Parameters.AddWithValue("$ilosc", (double)vm.Ilosc);
            regCmd.Parameters.AddWithValue("$cena", (double)vm.CenaNetto);
            regCmd.Parameters.AddWithValue("$vatId", vm.StawkaVatId);
            regCmd.Parameters.AddWithValue("$opis", string.IsNullOrWhiteSpace(vm.Opis) ? DBNull.Value : (object)vm.Opis);
            regCmd.Parameters.AddWithValue("$dostawca", string.IsNullOrWhiteSpace(vm.Dostawca) ? DBNull.Value : (object)vm.Dostawca);
            regCmd.Parameters.AddWithValue("$dataDostawy", string.IsNullOrWhiteSpace(vm.DataDostawy) ? DBNull.Value : (object)vm.DataDostawy);
            regCmd.Parameters.AddWithValue("$userId", userId);
            regCmd.ExecuteNonQuery();
        }

        _logger.LogInformation("[MAG-UC1] '{User}' zarejestrował towar '{Towar}'", SL(User.Identity?.Name), SL(vm.NazwaTowaru));
        TempData["SuccessMessage"] = "Towar został poprawnie zarejestrowany";
        return RedirectToAction(nameof(StanyMagazynowe));
    }
}
