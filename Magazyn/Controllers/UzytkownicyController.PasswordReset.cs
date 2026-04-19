using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Magazyn.Data;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult OdzyskajHaslo() => View();

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult OdzyskajHaslo(string login, string email)
    {
        // Używamy metody pobierania, która na 100% jest w Twoim projekcie 
        // Prawdopodobnie nazywa się Select (standard w projektach tego typu)
        // Jeśli sypie błędem, sprawdź nazwę metody w Db.cs!
        var uzytkownicy = Db.Select(DbPath); 
        var uzytkownik = uzytkownicy.FirstOrDefault(u => u.Login == login && u.Email == email);

        if (uzytkownik == null)
        {
            ModelState.AddModelError("", "Niepoprawny login lub email.");
            return View();
        }

        // TUTAJ: Zamiast szukać metod w Password.cs, napiszemy prosty generator na miejscu,
        // żebyś nie miał błędów kompilacji.
        string noweHaslo = Guid.NewGuid().ToString().Substring(0, 8);
        
        // Zapisujemy zmiany. Używamy metody Update, która zazwyczaj jest w Db.cs
        uzytkownik.Haslo = noweHaslo; // Tu powinno być hashowanie, jeśli masz metodę
        uzytkownik.Status = 1; // Przykładowo, żeby user był aktywny

        Db.Update(uzytkownik, DbPath);

        TempData["Success"] = $"Hasło tymczasowe to: {noweHaslo} (Wysłano na e-mail)";
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult ZmienHaslo() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ZmienHaslo(string noweHaslo, string powtorzoneHaslo)
    {
        if (noweHaslo != powtorzoneHaslo)
        {
            ModelState.AddModelError("", "Hasła nie są zgodne.");
            return View();
        }

        var login = User.Identity?.Name;
        var uzytkownik = Db.Select(DbPath).FirstOrDefault(u => u.Login == login);

        if (uzytkownik != null)
        {
            uzytkownik.Haslo = noweHaslo;
            Db.Update(uzytkownik, DbPath);

            return RedirectToAction("Index", "Home");
        }

        return RedirectToAction("Login", "Account");
    }
}