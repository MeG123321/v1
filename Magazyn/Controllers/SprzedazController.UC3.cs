using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class SprzedazController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik sprzedazy")]
    public IActionResult SzczegolySprzedazy(long id)
    {
        if (!System.IO.File.Exists(DbPath))
        {
            TempData["ErrorMessage"] = "Nie znaleziono szczegółów sprzedaży.";
            return RedirectToAction(nameof(HistoriaSprzedazy));
        }

        using var conn = Db.OpenConnection(DbPath);

        SzczegolySprzedazyVm? vm = null;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT s.Id, s.Nabywca, s.Adres, s.DataSprzedazy, s.DataRejestracji,
       u.firstName || ' ' || u.LastName AS Sprzedawca
FROM Sprzedaze s
JOIN Uzytkownicy u ON u.id = s.SprzedawcaUserId
WHERE s.Id = $id";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                TempData["ErrorMessage"] = "Nie znaleziono szczegółów sprzedaży.";
                return RedirectToAction(nameof(HistoriaSprzedazy));
            }

            vm = new SzczegolySprzedazyVm
            {
                Id = Convert.ToInt64(reader["Id"]),
                NazwaKlienta = reader["Nabywca"].ToString()!,
                AdresKlienta = reader["Adres"].ToString()!,
                DataSprzedazy = reader["DataSprzedazy"].ToString()!,
                DataRejestracji = reader["DataRejestracji"].ToString()!,
                Sprzedawca = reader["Sprzedawca"].ToString()!
            };
        }

        using (var itemsCmd = conn.CreateCommand())
        {
            itemsCmd.CommandText = @"
SELECT t.NazwaTowaru, jm.Nazwa AS JednostkaMiary, sp.Ilosc
FROM SprzedazPozycje sp
JOIN Towary t ON t.Id = sp.TowarId
JOIN JednostkiMiary jm ON jm.Id = t.JednostkaMiaryId
WHERE sp.SprzedazId = $id
ORDER BY t.NazwaTowaru";
            itemsCmd.Parameters.AddWithValue("$id", id);
            using var itemsReader = itemsCmd.ExecuteReader();
            while (itemsReader.Read())
            {
                vm.Pozycje.Add(new SprzedazPozycjaSzczegolDto
                {
                    NazwaTowaru = itemsReader["NazwaTowaru"].ToString()!,
                    JednostkaMiary = itemsReader["JednostkaMiary"].ToString()!,
                    Ilosc = Convert.ToDecimal(itemsReader["Ilosc"])
                });
            }
        }

        return View(vm);
    }
}
