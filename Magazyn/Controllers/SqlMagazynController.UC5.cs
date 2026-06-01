using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class MagazynController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult ZmianaVat(long? towarId = null, long? rodzajId = null)
    {
        if (!System.IO.File.Exists(DbPath)) return View(new ZmianaVatVm());
        using var connection = Db.OpenConnection(DbPath);
        var viewModel = new ZmianaVatVm
        {
            StawkiVat = GetStawkiVat(connection)
        };

        if (towarId.HasValue)
        {
            viewModel.Zakres = "TOWAR";
            viewModel.TowarId = towarId;
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
            command.Parameters.AddWithValue("$id", towarId.Value);
            viewModel.NazwaTowaru = command.ExecuteScalar()?.ToString() ?? "";
        }
        else if (rodzajId.HasValue)
        {
            viewModel.Zakres = "RODZAJ";
            viewModel.RodzajId = rodzajId;
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Nazwa FROM TowarRodzaje WHERE Id = $id";
            command.Parameters.AddWithValue("$id", rodzajId.Value);
            viewModel.NazwaRodzaju = command.ExecuteScalar()?.ToString() ?? "";
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult ZmianaVat(ZmianaVatVm viewModel)
    {
        using var connection = Db.OpenConnection(DbPath);

        if (!DateTime.TryParse(viewModel.DataObowiazywania, out var dataObowiazywania) || dataObowiazywania.Date <= DateTime.Today)
        {
            ModelState.AddModelError("DataObowiazywania", "Data obowiązywania musi być datą przyszłą");
            viewModel.StawkiVat = GetStawkiVat(connection);
            if (viewModel.TowarId.HasValue)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
                command.Parameters.AddWithValue("$id", viewModel.TowarId.Value);
                viewModel.NazwaTowaru = command.ExecuteScalar()?.ToString() ?? "";
            }
            else if (viewModel.RodzajId.HasValue)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Nazwa FROM TowarRodzaje WHERE Id = $id";
                command.Parameters.AddWithValue("$id", viewModel.RodzajId.Value);
                viewModel.NazwaRodzaju = command.ExecuteScalar()?.ToString() ?? "";
            }
            return View(viewModel);
        }

        if (!ModelState.IsValid)
        {
            viewModel.StawkiVat = GetStawkiVat(connection);
            return View(viewModel);
        }

        var userId = GetCurrentUserId();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
INSERT INTO PlanowaneZmianyVat (Zakres, TowarId, RodzajId, NowaStawkaVatId, DataObowiazywania, CreatedAt, CreatedByUserId)
VALUES ($zakres, $towarId, $rodzajId, $vatId, $data, datetime('now'), $userId)";
            command.Parameters.AddWithValue("$zakres", viewModel.Zakres);
            command.Parameters.AddWithValue("$towarId", viewModel.TowarId.HasValue ? (object)viewModel.TowarId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$rodzajId", viewModel.RodzajId.HasValue ? (object)viewModel.RodzajId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$vatId", viewModel.NowaStawkaVatId);
            command.Parameters.AddWithValue("$data", viewModel.DataObowiazywania);
            command.Parameters.AddWithValue("$userId", userId);
            command.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = $"Zmiana stawki VAT została zaplanowana i zacznie obowiązywać od dnia {viewModel.DataObowiazywania}";
        return RedirectToAction(nameof(StanyMagazynowe));
    }
}
