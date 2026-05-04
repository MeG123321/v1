using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class MagazynController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult SzczegolyRejestracji(long id)
    {
        if (!System.IO.File.Exists(DbPath))
        {
            TempData["ErrorMessage"] = "Nie znaleziono szczegółowych danych dla wybranego towaru";
            return RedirectToAction(nameof(StanyMagazynowe));
        }

        using var conn = Db.OpenConnection(DbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT rt.Id, t.NazwaTowaru, r.Nazwa AS RodzajTowaru, jm.Nazwa AS JednostkaMiary,
       rt.Ilosc, rt.CenaNetto, sv.Nazwa AS StawkaVat,
       rt.Opis, rt.Dostawca, rt.DataDostawy, rt.DataRejestracji,
       u.firstName || ' ' || u.LastName AS ImieNazwisko
FROM RejestracjeTowaru rt
JOIN Towary t ON t.Id = rt.TowarId
JOIN TowarRodzaje r ON r.Id = t.RodzajId
JOIN JednostkiMiary jm ON jm.Id = t.JednostkaMiaryId
JOIN StawkiVat sv ON sv.Id = rt.StawkaVatId
JOIN Uzytkownicy u ON u.id = rt.RejestrujacyUserId
WHERE rt.Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            TempData["ErrorMessage"] = "Nie znaleziono szczegółowych danych dla wybranego towaru";
            return RedirectToAction(nameof(StanyMagazynowe));
        }

        var vm = new SzczegolyRejestracjiVm
        {
            Id = Convert.ToInt64(reader["Id"]),
            NazwaTowaru = reader["NazwaTowaru"].ToString()!,
            RodzajTowaru = reader["RodzajTowaru"].ToString()!,
            JednostkaMiary = reader["JednostkaMiary"].ToString()!,
            Ilosc = Convert.ToDecimal(reader["Ilosc"]),
            CenaNetto = Convert.ToDecimal(reader["CenaNetto"]),
            StawkaVat = reader["StawkaVat"].ToString()!,
            Opis = reader["Opis"] is DBNull ? null : reader["Opis"].ToString(),
            Dostawca = reader["Dostawca"] is DBNull ? null : reader["Dostawca"].ToString(),
            DataDostawy = reader["DataDostawy"] is DBNull ? null : reader["DataDostawy"].ToString(),
            DataRejestracji = reader["DataRejestracji"].ToString()!,
            ImieNazwiskoPracownika = reader["ImieNazwisko"].ToString()!
        };

        return View(vm);
    }
}
