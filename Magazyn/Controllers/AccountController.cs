using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using System.Net;
using System.Net.Mail;
using Magazyn.Security;

namespace Magazyn.Controllers;

public class AccountController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;
    private const int MaxFailedAttempts = 3;
    private const int LockoutMinutes = 15;

    public AccountController(IWebHostEnvironment env, ILogger<AccountController> logger)
    {
        _env = env;
        _logger = logger;
    }

    private string DbPath => Db.GetDbPath(_env);

    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        using var connection = Db.OpenConnection(DbPath);
        UserAuthDto? user = GetUserForAuth(connection, model.Username);

        if (user == null)
        {
            ModelState.AddModelError("", "Niepoprawny login lub hasło");
            return View(model);
        }

        if (user.BlokadaDo.HasValue && user.BlokadaDo.Value > DateTime.Now)
        {
            ModelState.AddModelError("", $"Konto zablokowane do godziny: {user.BlokadaDo.Value:HH:mm}");
            return View(model);
        }

        if (user.Password != model.Password) 
        {
            HandleFailedLogin(connection, user);
            ModelState.AddModelError("", "Niepoprawny login lub hasło");
            return View(model);
        }

        ResetLoginAttempts(connection, user.Id);
        var roles = GetUserRoles(connection, user.Id);
        await SignInUser(user, roles, model.RememberMe);

        if (user.CzyHasloTymczasowe)
            return RedirectToAction("ChangePassword");

        // Przekierowanie: Jeśli masz jakąkolwiek rolę zarządzającą, idziesz do AdminPanel
        if (roles.Any(r => r == "Administrator" || r == "Kierownik sprzedazy" || r == "Kierownik magazynu"))
        {
            return RedirectToAction("AdminPanel", "Uzytkownicy");
        }
        
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    private UserAuthDto? GetUserForAuth(System.Data.IDbConnection conn, string login)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, Email, Password, liczba_blednych_logowan, blokada_do, czy_haslo_tymczasowe FROM Uzytkownicy WHERE LOWER(username) = LOWER($login) AND czy_zapomniany = 0 LIMIT 1";
        var p = cmd.CreateParameter(); p.ParameterName = "$login"; p.Value = login.Trim(); cmd.Parameters.Add(p);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new UserAuthDto {
            Id = Convert.ToInt64(r["id"]),
            Username = r["username"].ToString()!,
            Email = r["Email"].ToString()!,
            Password = r["Password"].ToString()!,
            LiczbaBledow = Convert.ToInt32(r["liczba_blednych_logowan"]),
            BlokadaDo = r["blokada_do"] is DBNull ? null : DateTime.Parse(r["blokada_do"].ToString()!),
            CzyHasloTymczasowe = Convert.ToInt32(r["czy_haslo_tymczasowe"]) == 1
        };
    }

    private List<string> GetUserRoles(System.Data.IDbConnection conn, long userId)
    {
        var roles = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT p.Nazwa FROM Uprawnienia p JOIN Uzytkownik_Uprawnienia uu ON p.Id = uu.uprawnienie_id WHERE uu.uzytkownik_id = $id";
        var p = cmd.CreateParameter(); p.ParameterName = "$id"; p.Value = userId; cmd.Parameters.Add(p);
        using var r = cmd.ExecuteReader();
        while (r.Read()) roles.Add(r.GetString(0).Trim());
        return roles;
    }

    private async Task SignInUser(UserAuthDto user, List<string> roles, bool isPersistent)
    {
        var claims = new List<Claim> {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };
        foreach (var role in roles) claims.Add(new Claim(ClaimTypes.Role, role));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = isPersistent });
    }

    private void HandleFailedLogin(System.Data.IDbConnection conn, UserAuthDto user) { /* Logika blokady */ }
    private void ResetLoginAttempts(System.Data.IDbConnection conn, long userId) { /* Reset prób */ }
}

public class UserAuthDto {
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public int LiczbaBledow { get; set; }
    public DateTime? BlokadaDo { get; set; }
    public bool CzyHasloTymczasowe { get; set; }
}