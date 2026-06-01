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
        var viewModel = new RejestracjaSprzedazyVm
        {
            DataSprzedazy = DateTime.Today.ToString("yyyy-MM-dd")
        };

        if (!System.IO.File.Exists(DbPath)) return View(viewModel);

        using var connection = Db.OpenConnection(DbPath);
        viewModel.Pozycje = GetDostepneTowary(connection);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Sprzedawca")]
    public IActionResult RejestracjaSprzedazy(RejestracjaSprzedazyVm viewModel)
    {
        if (!System.IO.File.Exists(DbPath))
        {
            TempData["ErrorMessage"] = "Nie znaleziono bazy danych do rejestracji sprzedaży.";
            return RedirectToAction(nameof(RejestracjaSprzedazy));
        }

        using var connection = Db.OpenConnection(DbPath);
        viewModel.Pozycje ??= new List<SprzedazPozycjaVm>();

        if (!DateTime.TryParse(viewModel.DataSprzedazy, out var dataSprzedazy))
        {
            ModelState.AddModelError("DataSprzedazy", "Niepoprawny format daty sprzedaży");
        }
        else if (dataSprzedazy.Date < DateTime.Today)
        {
            ModelState.AddModelError("DataSprzedazy", "Data sprzedaży nie może być wcześniejsza niż bieżąca");
        }

        var wybranePozycje = viewModel.Pozycje
            .Where(p => p.Ilosc.HasValue && p.Ilosc.Value > 0)
            .ToList();

        if (!wybranePozycje.Any())
            ModelState.AddModelError(string.Empty, "Dodaj przynajmniej jeden towar do sprzedaży.");

        if (!ModelState.IsValid)
        {
            ReloadPozycje(viewModel, connection);
            return View(viewModel);
        }

        var idParameters = wybranePozycje.Select((p, i) => new { p.TowarId, Param = $"$id{i}" }).ToList();
        var stanMagazynowyPoId = new Dictionary<long, decimal>();

        using (var stockCommand = connection.CreateCommand())
        {
            stockCommand.CommandText = $"SELECT Id, AktualnaIlosc FROM Towary WHERE Id IN ({string.Join(", ", idParameters.Select(p => p.Param))})";
            foreach (var param in idParameters)
                stockCommand.Parameters.AddWithValue(param.Param, param.TowarId);

            using var stockReader = stockCommand.ExecuteReader();
            while (stockReader.Read())
                stanMagazynowyPoId[Convert.ToInt64(stockReader["Id"])] = Convert.ToDecimal(stockReader["AktualnaIlosc"]);
        }

        for (int i = 0; i < viewModel.Pozycje.Count; i++)
        {
            var pozycja = viewModel.Pozycje[i];
            if (!pozycja.Ilosc.HasValue || pozycja.Ilosc.Value <= 0) continue;

            if (!stanMagazynowyPoId.TryGetValue(pozycja.TowarId, out var dostepna))
            {
                ModelState.AddModelError($"Pozycje[{i}].Ilosc", "Wybrany towar nie jest dostępny.");
                continue;
            }

            if (pozycja.Ilosc.Value > dostepna)
                ModelState.AddModelError($"Pozycje[{i}].Ilosc", "Brak wystarczającej ilości towaru.");
        }

        if (!ModelState.IsValid)
        {
            ReloadPozycje(viewModel, connection);
            return View(viewModel);
        }

        var userId = GetCurrentUserId();
        var nabywca = viewModel.NazwaKlienta.Trim();
        var adres = viewModel.AdresKlienta.Trim();
        var dataZapisu = dataSprzedazy.ToString("yyyy-MM-dd");

        using var transaction = connection.BeginTransaction();
        long sprzedazId;

        using (var sprzedazCommand = connection.CreateCommand())
        {
            sprzedazCommand.Transaction = transaction;
            sprzedazCommand.CommandText = @"
INSERT INTO Sprzedaze (Nabywca, Adres, DataSprzedazy, SprzedawcaUserId)
VALUES ($nabywca, $adres, $dataSprzedazy, $userId)";
            sprzedazCommand.Parameters.AddWithValue("$nabywca", nabywca);
            sprzedazCommand.Parameters.AddWithValue("$adres", adres);
            sprzedazCommand.Parameters.AddWithValue("$dataSprzedazy", dataZapisu);
            sprzedazCommand.Parameters.AddWithValue("$userId", userId);
            sprzedazCommand.ExecuteNonQuery();

            using var sprzedazIdCommand = connection.CreateCommand();
            sprzedazIdCommand.Transaction = transaction;
            sprzedazIdCommand.CommandText = "SELECT last_insert_rowid()";
            sprzedazId = Convert.ToInt64(sprzedazIdCommand.ExecuteScalar());
        }

        foreach (var pozycja in wybranePozycje)
        {
            using (var pozycjaCommand = connection.CreateCommand())
            {
                pozycjaCommand.Transaction = transaction;
                pozycjaCommand.CommandText = @"
INSERT INTO SprzedazPozycje (SprzedazId, TowarId, Ilosc)
VALUES ($sprzedazId, $towarId, $ilosc)";
                pozycjaCommand.Parameters.AddWithValue("$sprzedazId", sprzedazId);
                pozycjaCommand.Parameters.AddWithValue("$towarId", pozycja.TowarId);
                pozycjaCommand.Parameters.AddWithValue("$ilosc", (double)pozycja.Ilosc!.Value);
                pozycjaCommand.ExecuteNonQuery();
            }

            using (var aktualizacjaCommand = connection.CreateCommand())
            {
                aktualizacjaCommand.Transaction = transaction;
                aktualizacjaCommand.CommandText = "UPDATE Towary SET AktualnaIlosc = AktualnaIlosc - $ilosc WHERE Id = $towarId";
                aktualizacjaCommand.Parameters.AddWithValue("$ilosc", (double)pozycja.Ilosc!.Value);
                aktualizacjaCommand.Parameters.AddWithValue("$towarId", pozycja.TowarId);
                aktualizacjaCommand.ExecuteNonQuery();
            }
        }

        transaction.Commit();

        _logger.LogInformation("[SPRZ-UC1] '{User}' zarejestrował sprzedaż dla '{Nabywca}'", SL(User.Identity?.Name), SL(nabywca));
        TempData["SuccessMessage"] = "Sprzedaż została zarejestrowana";
        return RedirectToAction(nameof(RejestracjaSprzedazy));
    }
}
