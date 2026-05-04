using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class MagazynController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult RodzajeTowaru()
    {
        if (!System.IO.File.Exists(DbPath)) return View(new List<TowarRodzajVm>());
        using var conn = Db.OpenConnection(DbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT r.Id, r.Nazwa,
       (SELECT COUNT(*) FROM Towary t WHERE t.RodzajId = r.Id AND t.CzyAktywny = 1) AS LiczbaTowarow
FROM TowarRodzaje r
WHERE r.CzyAktywny = 1
ORDER BY r.Nazwa";
        var list = new List<TowarRodzajVm>();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
            list.Add(new TowarRodzajVm
            {
                Id = Convert.ToInt64(dr["Id"]),
                Nazwa = dr["Nazwa"].ToString()!,
                LiczbaTowarow = Convert.ToInt32(dr["LiczbaTowarow"])
            });
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult DodajRodzaj(string nazwa)
    {
        if (string.IsNullOrWhiteSpace(nazwa))
        {
            TempData["ErrorMessage"] = "Nazwa rodzaju jest wymagana";
            return RedirectToAction(nameof(RodzajeTowaru));
        }

        using var conn = Db.OpenConnection(DbPath);
        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM TowarRodzaje WHERE LOWER(TRIM(Nazwa)) = LOWER(TRIM($nazwa))";
            checkCmd.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
            {
                TempData["ErrorMessage"] = "Podany rodzaj towaru już znajduje się w systemie";
                return RedirectToAction(nameof(RodzajeTowaru));
            }
        }

        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = "INSERT INTO TowarRodzaje (Nazwa) VALUES ($nazwa)";
            insCmd.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            insCmd.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = "Nowy rodzaj towaru został dodany";
        return RedirectToAction(nameof(RodzajeTowaru));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult EdytujRodzaj(long id, string nazwa)
    {
        if (string.IsNullOrWhiteSpace(nazwa))
        {
            TempData["ErrorMessage"] = "Nazwa rodzaju jest wymagana";
            return RedirectToAction(nameof(RodzajeTowaru));
        }

        using var conn = Db.OpenConnection(DbPath);
        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM TowarRodzaje WHERE LOWER(TRIM(Nazwa)) = LOWER(TRIM($nazwa)) AND Id != $id";
            checkCmd.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            checkCmd.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
            {
                TempData["ErrorMessage"] = "Podany rodzaj towaru już znajduje się w systemie";
                return RedirectToAction(nameof(RodzajeTowaru));
            }
        }

        using (var updCmd = conn.CreateCommand())
        {
            updCmd.CommandText = "UPDATE TowarRodzaje SET Nazwa = $nazwa WHERE Id = $id";
            updCmd.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            updCmd.Parameters.AddWithValue("$id", id);
            updCmd.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = "Rodzaj towaru został zaktualizowany";
        return RedirectToAction(nameof(RodzajeTowaru));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult UsunRodzaj(long id)
    {
        using var conn = Db.OpenConnection(DbPath);
        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM Towary WHERE RodzajId = $id AND CzyAktywny = 1";
            checkCmd.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
            {
                TempData["ErrorMessage"] = "Nie można usunąć rodzaju przypisanego do towarów";
                return RedirectToAction(nameof(RodzajeTowaru));
            }
        }

        using (var delCmd = conn.CreateCommand())
        {
            delCmd.CommandText = "DELETE FROM TowarRodzaje WHERE Id = $id";
            delCmd.Parameters.AddWithValue("$id", id);
            delCmd.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = "Rodzaj towaru został usunięty";
        return RedirectToAction(nameof(RodzajeTowaru));
    }
}
