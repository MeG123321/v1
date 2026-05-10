using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using System.Linq;

namespace Magazyn.Controllers;

public partial class SprzedazController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Sprzedawca")]
    public IActionResult RejestracjaSprzedazy()
    {
        var vm = new RejestracjaSprzedazyVm
        {
            DataSprzedazy = DateTime.Today.ToString("yyyy-MM-dd")
        };

        if (!System.IO.File.Exists(DbPath)) return View(vm);

        using var conn = Db.OpenConnection(DbPath);
        vm.Pozycje = GetDostepneTowary(conn);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Sprzedawca")]
    public IActionResult RejestracjaSprzedazy(RejestracjaSprzedazyVm vm)
    {
        if (!System.IO.File.Exists(DbPath))
        {
            TempData["ErrorMessage"] = "Nie znaleziono bazy danych do rejestracji sprzedaży.";
            return RedirectToAction(nameof(RejestracjaSprzedazy));
        }

        using var conn = Db.OpenConnection(DbPath);
        vm.Pozycje ??= new List<SprzedazPozycjaVm>();

        if (!DateTime.TryParse(vm.DataSprzedazy, out var dataSprzedazy) || dataSprzedazy.Date < DateTime.Today)
            ModelState.AddModelError("DataSprzedazy", "Data sprzedaży nie może być wcześniejsza niż bieżąca");

        var wybranePozycje = vm.Pozycje
            .Where(p => p.Ilosc.HasValue && p.Ilosc.Value > 0)
            .ToList();

        if (!wybranePozycje.Any())
            ModelState.AddModelError(string.Empty, "Dodaj przynajmniej jeden towar do sprzedaży.");

        if (!ModelState.IsValid)
        {
            ReloadPozycje(vm, conn);
            return View(vm);
        }

        var idParams = wybranePozycje.Select((p, i) => new { p.TowarId, Param = $"$id{i}" }).ToList();
        var stockById = new Dictionary<long, decimal>();

        using (var stockCmd = conn.CreateCommand())
        {
            stockCmd.CommandText = $"SELECT Id, AktualnaIlosc FROM Towary WHERE Id IN ({string.Join(", ", idParams.Select(p => p.Param))})";
            foreach (var param in idParams)
                stockCmd.Parameters.AddWithValue(param.Param, param.TowarId);

            using var stockReader = stockCmd.ExecuteReader();
            while (stockReader.Read())
                stockById[Convert.ToInt64(stockReader["Id"])] = Convert.ToDecimal(stockReader["AktualnaIlosc"]);
        }

        for (int i = 0; i < vm.Pozycje.Count; i++)
        {
            var pozycja = vm.Pozycje[i];
            if (!pozycja.Ilosc.HasValue || pozycja.Ilosc.Value <= 0) continue;

            if (!stockById.TryGetValue(pozycja.TowarId, out var dostepna))
            {
                ModelState.AddModelError($"Pozycje[{i}].Ilosc", "Wybrany towar nie jest dostępny.");
                continue;
            }

            if (pozycja.Ilosc.Value > dostepna)
                ModelState.AddModelError($"Pozycje[{i}].Ilosc", "Brak wystarczającej ilości towaru.");
        }

        if (!ModelState.IsValid)
        {
            ReloadPozycje(vm, conn);
            return View(vm);
        }

        var userId = GetCurrentUserId();
        var nabywca = vm.NazwaKlienta.Trim();
        var adres = vm.AdresKlienta.Trim();
        var dataZapisu = dataSprzedazy.ToString("yyyy-MM-dd");

        using var transaction = conn.BeginTransaction();
        long sprzedazId;

        using (var saleCmd = conn.CreateCommand())
        {
            saleCmd.Transaction = transaction;
            saleCmd.CommandText = @"
INSERT INTO Sprzedaze (Nabywca, Adres, DataSprzedazy, SprzedawcaUserId)
VALUES ($nabywca, $adres, $dataSprzedazy, $userId)";
            saleCmd.Parameters.AddWithValue("$nabywca", nabywca);
            saleCmd.Parameters.AddWithValue("$adres", adres);
            saleCmd.Parameters.AddWithValue("$dataSprzedazy", dataZapisu);
            saleCmd.Parameters.AddWithValue("$userId", userId);
            saleCmd.ExecuteNonQuery();

            using var idCmd = conn.CreateCommand();
            idCmd.Transaction = transaction;
            idCmd.CommandText = "SELECT last_insert_rowid()";
            sprzedazId = Convert.ToInt64(idCmd.ExecuteScalar());
        }

        foreach (var pozycja in wybranePozycje)
        {
            using (var itemCmd = conn.CreateCommand())
            {
                itemCmd.Transaction = transaction;
                itemCmd.CommandText = @"
INSERT INTO SprzedazPozycje (SprzedazId, TowarId, Ilosc)
VALUES ($sprzedazId, $towarId, $ilosc)";
                itemCmd.Parameters.AddWithValue("$sprzedazId", sprzedazId);
                itemCmd.Parameters.AddWithValue("$towarId", pozycja.TowarId);
                itemCmd.Parameters.AddWithValue("$ilosc", (double)pozycja.Ilosc!.Value);
                itemCmd.ExecuteNonQuery();
            }

            using (var updateCmd = conn.CreateCommand())
            {
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = "UPDATE Towary SET AktualnaIlosc = AktualnaIlosc - $ilosc WHERE Id = $towarId";
                updateCmd.Parameters.AddWithValue("$ilosc", (double)pozycja.Ilosc!.Value);
                updateCmd.Parameters.AddWithValue("$towarId", pozycja.TowarId);
                updateCmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();

        _logger.LogInformation("[SPRZ-UC1] '{User}' zarejestrował sprzedaż dla '{Nabywca}'", SL(User.Identity?.Name), SL(nabywca));
        TempData["SuccessMessage"] = "Sprzedaż została zarejestrowana";
        return RedirectToAction(nameof(RejestracjaSprzedazy));
    }
}
