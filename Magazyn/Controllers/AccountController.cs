using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using Magazyn.Security; // Tu znajduje się PasswordGenerator

namespace Magazyn.Controllers;

public class AccountController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _config;

    private const int MaxFailedAttempts = 3;
    private const int LockoutMinutes = 15;

    public AccountController(IWebHostEnvironment env, ILogger<AccountController> logger, IConfiguration config)
    {
        _env = env;
        _logger = logger;
        _config = config;
    }

    private string DbPath => Db.GetDbPath(_env);
    private static string SL(string? value) => (value ?? "").Replace('\r', '_').Replace('\n', '_');

    // ==========================================
    // LG_UC1: LOGOWANIE
    // ==========================================
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        using var connection = Db.OpenConnection(DbPath);
        UserAuthDto? user = GetUserForAuth(connection, model.Username);

        // NAPRAWA CS0019: Użycie 'is null' zamiast '== null'
        if (user is null)
        {
            ModelState.AddModelError("", "Niepoprawny login lub hasło");
            return View(model);
        }

        // Sprawdzenie blokady czasowej
        if (user.BlokadaDo.HasValue && user.BlokadaDo.Value > DateTime.Now)
        {
            ModelState.AddModelError("", $"Konto zablokowane do godziny: {user.BlokadaDo.Value:HH:mm}");
            return View(model);
        }

        // Weryfikacja hasła
        if (user.Password != model.Password)
        {
            HandleFailedLogin(connection, user);
            ModelState.AddModelError("", "Niepoprawny login lub hasło");
            return View(model);
        }

        // Sukces logowania - reset licznika i sesja
        ResetLoginAttempts(connection, user.Id);
        var roles = GetUserRoles(connection, user.Id);
        await SignInUser(user, roles, model.RememberMe);

        _logger.LogInformation("[Auth] Zalogowano użytkownika: {Username}", SL(user.Username));

        // LG_UC4: Wymuszona zmiana hasła przy hasle tymczasowym
        if (user.CzyHasloTymczasowe)
            return RedirectToAction("ChangePassword");

        return RedirectToAction("AdminPanel", "Uzytkownicy");
    }

    // ==========================================
    // LG_UC2: WYLOGOWANIE
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // ==========================================
    // LG_UC3: ODZYSKIWANIE HASŁA (UŻYTKOWNIK)
    // ==========================================
    [HttpGet]
    public IActionResult RecoverPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecoverPassword(RecoverPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, Email FROM Uzytkownicy WHERE username = $user AND Email = $email AND czy_zapomniany = 0 LIMIT 1";
        cmd.Parameters.AddWithValue("$user", model.Username);
        cmd.Parameters.AddWithValue("$email", model.Email);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            ModelState.AddModelError("", "Niepoprawne dane. Login lub e-mail są nieprawidłowe.");
            return View(model);
        }

        long userId = Convert.ToInt64(reader["id"]);
        string userEmail = reader["Email"].ToString()!;
        reader.Close();

        // GENEROWANIE: Twoja funkcja PasswordGenerator
        string tempPass = PasswordGenerator.Generate(10);
        UpdatePasswordInDb(connection, userId, tempPass, true);

        bool sent = await SendEmail(userEmail, tempPass);
        TempData["SuccessMessage"] = sent 
            ? "Nowe hasło tymczasowe zostało wysłane na Twój adres e-mail." 
            : $"[BŁĄD WYSYŁKI] Twoje hasło tymczasowe to: {tempPass}";

        return View();
    }

    // ==========================================
    // LG_UC5: GENEROWANIE HASŁA (ADMINISTRATOR)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Administrator,Kierownik magazynu")]
    public async Task<IActionResult> AdminGeneratePassword(long id)
    {
        using var connection = Db.OpenConnection(DbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Email FROM Uzytkownicy WHERE id = $id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", id);
        
        string? email = cmd.ExecuteScalar()?.ToString();
        if (string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "Brak adresu e-mail dla tego użytkownika.";
            return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
        }

        // GENEROWANIE: Twoja funkcja PasswordGenerator
        string tempPass = PasswordGenerator.Generate(10);
        UpdatePasswordInDb(connection, id, tempPass, true);

        bool sent = await SendEmail(email, tempPass);
        TempData["SuccessMessage"] = sent 
            ? "Hasło zostało wygenerowane i wysłane na e-mail użytkownika." 
            : $"[BŁĄD WYSYŁKI] Wygenerowane hasło: {tempPass}";

        return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
    }

    // ==========================================
    // PROFIL UŻYTKOWNIKA
    // ==========================================
    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult MyProfile() => View();

    // ==========================================
    // LG_UC4: ZMIANA HASŁA (WYMAGANA / DOBROWOLNA)
    // ==========================================
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return RedirectToAction("Login");

        using var connection = Db.OpenConnection(DbPath);
        UpdatePasswordInDb(connection, Convert.ToInt64(userIdClaim.Value), model.NewPassword, false);

        return RedirectToAction("AdminPanel", "Uzytkownicy");
    }

    // ==========================================
    // METODY POMOCNICZE
    // ==========================================

    private void UpdatePasswordInDb(System.Data.IDbConnection conn, long userId, string pass, bool isTemp)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Uzytkownicy SET Password = $pass, czy_haslo_tymczasowe = $temp, liczba_blednych_logowan = 0, blokada_do = NULL WHERE id = $id";
        
        var p1 = cmd.CreateParameter(); p1.ParameterName = "$pass"; p1.Value = pass; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "$temp"; p2.Value = isTemp ? 1 : 0; cmd.Parameters.Add(p2);
        var p3 = cmd.CreateParameter(); p3.ParameterName = "$id"; p3.Value = userId; cmd.Parameters.Add(p3);
        
        cmd.ExecuteNonQuery();
    }

   private async Task<bool> SendEmail(string targetEmail, string password)
{
    try
    {
        using var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new System.Net.NetworkCredential("magazynfirma123321@gmail.com", "rznmictqzjgewklh"),
            EnableSsl = true
        };

        var mailMessage = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress("magazynfirma123321@gmail.com", "Magazyn GiTA"),
            Subject = "Odzyskiwanie hasła",
            Body = $"nowe hasło: {password}",
            IsBodyHtml = false
        };

        mailMessage.To.Add(targetEmail);

        await client.SendMailAsync(mailMessage);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd SMTP");
        return false;
    }
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

    private void HandleFailedLogin(System.Data.IDbConnection conn, UserAuthDto user)
    {
        int newCount = user.LiczbaBledow + 1;
        object lockout = newCount >= MaxFailedAttempts ? DateTime.Now.AddMinutes(LockoutMinutes).ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value;
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Uzytkownicy SET liczba_blednych_logowan = $cnt, blokada_do = $lock WHERE id = $id";
        
        var p1 = cmd.CreateParameter(); p1.ParameterName = "$cnt"; p1.Value = newCount; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "$lock"; p2.Value = lockout; cmd.Parameters.Add(p2);
        var p3 = cmd.CreateParameter(); p3.ParameterName = "$id"; p3.Value = user.Id; cmd.Parameters.Add(p3);
        
        cmd.ExecuteNonQuery();
    }

    private void ResetLoginAttempts(System.Data.IDbConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Uzytkownicy SET liczba_blednych_logowan = 0, blokada_do = NULL WHERE id = $id";
        var p = cmd.CreateParameter(); p.ParameterName = "$id"; p.Value = userId; cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();
    }

    private List<string> GetUserRoles(System.Data.IDbConnection conn, long userId)
    {
        var roles = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT p.Nazwa FROM Uprawnienia p JOIN Uzytkownik_Uprawnienia uu ON p.Id = uu.uprawnienie_id WHERE uu.uzytkownik_id = $id";
        var p = cmd.CreateParameter(); p.ParameterName = "$id"; p.Value = userId; cmd.Parameters.Add(p);
        using var r = cmd.ExecuteReader();
        while (r.Read()) roles.Add(r.GetString(0));
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
}

public class UserAuthDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public int LiczbaBledow { get; set; }
    public DateTime? BlokadaDo { get; set; }
    public bool CzyHasloTymczasowe { get; set; }
}