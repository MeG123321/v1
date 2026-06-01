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
       using var connection = Db.OpenConnection(DbPath);
       using var command = connection.CreateCommand();
       command.CommandText = @"
SELECT r.Id, r.Nazwa,
       (SELECT COUNT(*) FROM Towary t WHERE t.RodzajId = r.Id AND t.CzyAktywny = 1) AS LiczbaTowarow
FROM TowarRodzaje r
WHERE r.CzyAktywny = 1
ORDER BY r.Nazwa";
       var rodzaje = new List<TowarRodzajVm>();
       using var reader = command.ExecuteReader();
        while (reader.Read())
           rodzaje.Add(new TowarRodzajVm
            {
                Id = Convert.ToInt64(reader["Id"]),
                Nazwa = reader["Nazwa"].ToString()!,
                LiczbaTowarow = Convert.ToInt32(reader["LiczbaTowarow"])
            });
       return View(rodzaje);
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

        using var connection = Db.OpenConnection(DbPath);
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(*) FROM TowarRodzaje WHERE LOWER(TRIM(Nazwa)) = LOWER(TRIM($nazwa))";
            checkCommand.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
            {
                TempData["ErrorMessage"] = "Podany rodzaj towaru już znajduje się w systemie";
                return RedirectToAction(nameof(RodzajeTowaru));
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = "INSERT INTO TowarRodzaje (Nazwa) VALUES ($nazwa)";
            insertCommand.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            insertCommand.ExecuteNonQuery();
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

        using var connection = Db.OpenConnection(DbPath);
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(*) FROM TowarRodzaje WHERE LOWER(TRIM(Nazwa)) = LOWER(TRIM($nazwa)) AND Id != $id";
            checkCommand.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            checkCommand.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
            {
                TempData["ErrorMessage"] = "Podany rodzaj towaru już znajduje się w systemie";
                return RedirectToAction(nameof(RodzajeTowaru));
            }
        }

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.CommandText = "UPDATE TowarRodzaje SET Nazwa = $nazwa WHERE Id = $id";
            updateCommand.Parameters.AddWithValue("$nazwa", nazwa.Trim());
            updateCommand.Parameters.AddWithValue("$id", id);
            updateCommand.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = "Rodzaj towaru został zaktualizowany";
        return RedirectToAction(nameof(RodzajeTowaru));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult UsunRodzaj(long id)
    {
        using var connection = Db.OpenConnection(DbPath);
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(*) FROM Towary WHERE RodzajId = $id AND CzyAktywny = 1";
            checkCommand.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
            {
                TempData["ErrorMessage"] = "Nie można usunąć rodzaju przypisanego do towarów";
                return RedirectToAction(nameof(RodzajeTowaru));
            }
        }

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM TowarRodzaje WHERE Id = $id";
            deleteCommand.Parameters.AddWithValue("$id", id);
            deleteCommand.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = "Rodzaj towaru został usunięty";
        return RedirectToAction(nameof(RodzajeTowaru));
    }
}
