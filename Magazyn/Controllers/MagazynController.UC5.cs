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
        using var conn = Db.OpenConnection(DbPath);
        var vm = new ZmianaVatVm
        {
            StawkiVat = GetStawkiVat(conn)
        };

        if (towarId.HasValue)
        {
            vm.Zakres = "TOWAR";
            vm.TowarId = towarId;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", towarId.Value);
            vm.NazwaTowaru = cmd.ExecuteScalar()?.ToString() ?? "";
        }
        else if (rodzajId.HasValue)
        {
            vm.Zakres = "RODZAJ";
            vm.RodzajId = rodzajId;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Nazwa FROM TowarRodzaje WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", rodzajId.Value);
            vm.NazwaRodzaju = cmd.ExecuteScalar()?.ToString() ?? "";
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult ZmianaVat(ZmianaVatVm vm)
    {
        using var conn = Db.OpenConnection(DbPath);

        if (!DateTime.TryParse(vm.DataObowiazywania, out var dataObowiazywania) || dataObowiazywania.Date <= DateTime.Today)
        {
            ModelState.AddModelError("DataObowiazywania", "Data obowiązywania musi być datą przyszłą");
            vm.StawkiVat = GetStawkiVat(conn);
            if (vm.TowarId.HasValue)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", vm.TowarId.Value);
                vm.NazwaTowaru = cmd.ExecuteScalar()?.ToString() ?? "";
            }
            else if (vm.RodzajId.HasValue)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Nazwa FROM TowarRodzaje WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", vm.RodzajId.Value);
                vm.NazwaRodzaju = cmd.ExecuteScalar()?.ToString() ?? "";
            }
            return View(vm);
        }

        if (!ModelState.IsValid)
        {
            vm.StawkiVat = GetStawkiVat(conn);
            return View(vm);
        }

        var userId = GetCurrentUserId();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO PlanowaneZmianyVat (Zakres, TowarId, RodzajId, NowaStawkaVatId, DataObowiazywania, CreatedAt, CreatedByUserId)
VALUES ($zakres, $towarId, $rodzajId, $vatId, $data, datetime('now'), $userId)";
            cmd.Parameters.AddWithValue("$zakres", vm.Zakres);
            cmd.Parameters.AddWithValue("$towarId", vm.TowarId.HasValue ? (object)vm.TowarId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$rodzajId", vm.RodzajId.HasValue ? (object)vm.RodzajId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$vatId", vm.NowaStawkaVatId);
            cmd.Parameters.AddWithValue("$data", vm.DataObowiazywania);
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = $"Zmiana stawki VAT została zaplanowana i zacznie obowiązywać od dnia {vm.DataObowiazywania}";
        return RedirectToAction(nameof(StanyMagazynowe));
    }
}
