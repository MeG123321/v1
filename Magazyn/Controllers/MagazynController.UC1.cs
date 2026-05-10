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

        // 1. Walidacja modelu
        if (!ModelState.IsValid)
        {
            ReloadVmLists(vm, conn);
            return View(vm);
        }

        // Przygotowanie nazwy (bezpieczne Trim)
        string nazwaTrimmed = vm.NazwaTowaru?.Trim() ?? string.Empty;

        // 2. Sprawdzenie, czy towar już istnieje (Blokada duplikatów)
        using (var findCmd = conn.CreateCommand())
        {
            findCmd.CommandText = "SELECT COUNT(1) FROM Towary WHERE LOWER(TRIM(NazwaTowaru)) = LOWER(TRIM($nazwa))";
            findCmd.Parameters.AddWithValue("$nazwa", nazwaTrimmed);
            
            var count = Convert.ToInt64(findCmd.ExecuteScalar());
            if (count > 0)
            {
                // Wyświetlamy komunikat o błędzie pod polem NazwaTowaru
                ModelState.AddModelError("NazwaTowaru", "Towar o podanej nazwie już istnieje.");
                ReloadVmLists(vm, conn);
                return View(vm);
            }
        }

        // 3. Rejestracja NOWEGO towaru
        var userId = GetCurrentUserId();
        long towarId;

        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = "INSERT INTO Towary (NazwaTowaru, RodzajId, JednostkaMiaryId, AktualnaIlosc) VALUES ($nazwa, $rodzajId, $jmId, $ilosc)";
            insCmd.Parameters.AddWithValue("$nazwa", nazwaTrimmed);
            insCmd.Parameters.AddWithValue("$rodzajId", vm.RodzajId);
            insCmd.Parameters.AddWithValue("$jmId", vm.JednostkaMiaryId);
            // POPRAWKA: Użycie .Value rozwiązuje błąd CS8629 dla decimal?
            insCmd.Parameters.AddWithValue("$ilosc", (double)vm.Ilosc!.Value); 
            insCmd.ExecuteNonQuery();

            using var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid()";
            towarId = Convert.ToInt64(lastIdCmd.ExecuteScalar());
        }

        // 4. Zapisanie historii rejestracji (dostawy)
        using (var regCmd = conn.CreateCommand())
        {
            regCmd.CommandText = @"
                INSERT INTO RejestracjeTowaru 
                (TowarId, Ilosc, CenaNetto, StawkaVatId, Opis, Dostawca, DataDostawy, DataRejestracji, RejestrujacyUserId) 
                VALUES 
                ($towarId, $ilosc, $cena, $vatId, $opis, $dostawca, $dataDostawy, datetime('now'), $userId)";
            
            regCmd.Parameters.AddWithValue("$towarId", towarId);
            // POPRAWKA: Użycie .Value dla Ilosc i CenaNetto
            regCmd.Parameters.AddWithValue("$ilosc", (double)vm.Ilosc!.Value);
            regCmd.Parameters.AddWithValue("$cena", (double)vm.CenaNetto!.Value);
            regCmd.Parameters.AddWithValue("$vatId", vm.StawkaVatId);
            regCmd.Parameters.AddWithValue("$opis", string.IsNullOrWhiteSpace(vm.Opis) ? DBNull.Value : (object)vm.Opis);
            regCmd.Parameters.AddWithValue("$dostawca", string.IsNullOrWhiteSpace(vm.Dostawca) ? DBNull.Value : (object)vm.Dostawca);
            regCmd.Parameters.AddWithValue("$dataDostawy", string.IsNullOrWhiteSpace(vm.DataDostawy) ? DBNull.Value : (object)vm.DataDostawy);
            regCmd.Parameters.AddWithValue("$userId", userId);
            regCmd.ExecuteNonQuery();
        }

        _logger.LogInformation("[MAG-UC1] '{User}' zarejestrował towar '{Towar}'", SL(User.Identity?.Name), SL(nazwaTrimmed));
        TempData["SuccessMessage"] = "Towar został poprawnie zarejestrowany";
        
        return RedirectToAction(nameof(StanyMagazynowe));
    }

    // Metoda pomocnicza, żeby nie powtarzać ładowania list
    private void ReloadVmLists(RejestracjaTowaruVm vm, System.Data.Common.DbConnection conn)
    {
        vm.Rodzaje = GetRodzaje(conn);
        vm.JednostkiMiary = GetJednostkiMiary(conn);
        vm.StawkiVat = GetStawkiVat(conn);
    }
}