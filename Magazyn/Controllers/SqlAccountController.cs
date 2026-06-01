using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using Magazyn.Security;

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

    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        using var connection = Db.OpenConnection(DbPath);
        UserAuthDto? user = GetUserForAuth(connection, model.Username);

        if (user is null)
        {
            ModelState.AddModelError("", "Niepoprawny login lub hasło");
            return View(model);
        }
       if (user.Status == 0)
{
    ModelState.AddModelError("", "Twoje konto jest nieaktywne.");
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

        _logger.LogInformation("[Auth] Zalogowano użytkownika: {Username}", SL(user.Username));

        if (user.CzyHasloTymczasowe)
            return RedirectToAction("ChangePassword");

        return RedirectToAction("AdminPanel", "Uzytkownicy");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

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

        string tempPass = PasswordGenerator.Generate(10);
        UpdatePasswordInDb(connection, userId, tempPass, true);

        bool sent = await SendEmail(userEmail, tempPass);
        TempData["SuccessMessage"] = sent 
            ? "Nowe hasło tymczasowe zostało wysłane na Twój adres e-mail." 
            : $"[BŁĄD WYSYŁKI] Twoje hasło tymczasowe to: {tempPass}";

        return View();
    }

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

        string tempPass = PasswordGenerator.Generate(10);
        UpdatePasswordInDb(connection, id, tempPass, true);

        bool sent = await SendEmail(email, tempPass);
        TempData["SuccessMessage"] = sent 
            ? "Hasło zostało wygenerowane i wysłane na e-mail użytkownika." 
            : $"[BŁĄD WYSYŁKI] Wygenerowane hasło: {tempPass}";

        return RedirectToAction("UserDetails", "Uzytkownicy", new { id });
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult MyProfile() => View();

    [HttpGet]
    public IActionResult ChangePassword() => View();

    [HttpPost]
[ValidateAntiForgeryToken]
public IActionResult ChangePassword(ChangePasswordViewModel model)
{
    if (!ModelState.IsValid) return View(model);

    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim is null) return RedirectToAction("Login");

    long userId = Convert.ToInt64(userIdClaim.Value);

    using var connection = Db.OpenConnection(DbPath);

    if (WasPasswordUsedRecently(connection, userId, model.NewPassword, 3))
    {
        ModelState.AddModelError("", "Nowe hasło musi różnić się od 3 ostatnich haseł");
        return View(model);
    }

    UpdatePasswordInDb(connection, userId, model.NewPassword, false);

    return RedirectToAction("AdminPanel", "Uzytkownicy");
}
private bool WasPasswordUsedRecently(System.Data.IDbConnection conn, long userId, string newPassword, int lastN)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT haslo_hash
        FROM Historia_Hasel
        WHERE uzytkownik_id = $uid
        ORDER BY datetime(data_nadania) DESC
        LIMIT $n";

    var userIdParameter = cmd.CreateParameter();
    userIdParameter.ParameterName = "$uid";
    userIdParameter.Value = userId;
    cmd.Parameters.Add(userIdParameter);

    var limitParameter = cmd.CreateParameter();
    limitParameter.ParameterName = "$n";
    limitParameter.Value = lastN;
    cmd.Parameters.Add(limitParameter);

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var previousPassword = reader["haslo_hash"]?.ToString() ?? "";
        if (previousPassword == newPassword)
            return true;
    }
    return false;
}
    private void UpdatePasswordInDb(System.Data.IDbConnection conn, long userId, string password, bool isTemporary)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Uzytkownicy SET Password = $pass, czy_haslo_tymczasowe = $temp, liczba_blednych_logowan = 0, blokada_do = NULL WHERE id = $id";
        
        var passwordParameter = cmd.CreateParameter();
        passwordParameter.ParameterName = "$pass";
        passwordParameter.Value = password;
        cmd.Parameters.Add(passwordParameter);

        var tempFlagParameter = cmd.CreateParameter();
        tempFlagParameter.ParameterName = "$temp";
        tempFlagParameter.Value = isTemporary ? 1 : 0;
        cmd.Parameters.Add(tempFlagParameter);

        var userIdParameter = cmd.CreateParameter();
        userIdParameter.ParameterName = "$id";
        userIdParameter.Value = userId;
        cmd.Parameters.Add(userIdParameter);
        
        cmd.ExecuteNonQuery();

        using var historyCommand = conn.CreateCommand();
        historyCommand.CommandText = @"
    INSERT INTO Historia_Hasel (uzytkownik_id, haslo_hash, data_nadania)
    VALUES ($uid, $pass, datetime('now'))";
        var historyUserIdParameter = historyCommand.CreateParameter();
        historyUserIdParameter.ParameterName = "$uid";
        historyUserIdParameter.Value = userId;
        historyCommand.Parameters.Add(historyUserIdParameter);

        var historyPasswordParameter = historyCommand.CreateParameter();
        historyPasswordParameter.ParameterName = "$pass";
        historyPasswordParameter.Value = password;
        historyCommand.Parameters.Add(historyPasswordParameter);
        historyCommand.ExecuteNonQuery();
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
    cmd.CommandText = @"
SELECT id, username, Email, Password,
       liczba_blednych_logowan, blokada_do, czy_haslo_tymczasowe,
       Status
FROM Uzytkownicy
WHERE LOWER(username) = LOWER($login)
  AND czy_zapomniany = 0
LIMIT 1";
    var loginParameter = cmd.CreateParameter();
    loginParameter.ParameterName = "$login";
    loginParameter.Value = login.Trim();
    cmd.Parameters.Add(loginParameter);

    using var reader = cmd.ExecuteReader();
    if (!reader.Read()) return null;

    return new UserAuthDto {
        Id = Convert.ToInt64(reader["id"]),
        Username = reader["username"].ToString()!,
        Email = reader["Email"].ToString()!,
        Password = reader["Password"].ToString()!,
        LiczbaBledow = Convert.ToInt32(reader["liczba_blednych_logowan"]),
        BlokadaDo = reader["blokada_do"] is DBNull ? null : DateTime.Parse(reader["blokada_do"].ToString()!),
        CzyHasloTymczasowe = Convert.ToInt32(reader["czy_haslo_tymczasowe"]) == 1,
        Status = reader["Status"] is DBNull ? 0 : Convert.ToInt32(reader["Status"])
    };
}

    private void HandleFailedLogin(System.Data.IDbConnection conn, UserAuthDto user)
    {
        int newCount = user.LiczbaBledow + 1;
        object lockoutValue = newCount >= MaxFailedAttempts ? DateTime.Now.AddMinutes(LockoutMinutes).ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value;
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Uzytkownicy SET liczba_blednych_logowan = $cnt, blokada_do = $lock WHERE id = $id";
        
        var failedCountParameter = cmd.CreateParameter();
        failedCountParameter.ParameterName = "$cnt";
        failedCountParameter.Value = newCount;
        cmd.Parameters.Add(failedCountParameter);

        var lockoutParameter = cmd.CreateParameter();
        lockoutParameter.ParameterName = "$lock";
        lockoutParameter.Value = lockoutValue;
        cmd.Parameters.Add(lockoutParameter);

        var userIdParameter = cmd.CreateParameter();
        userIdParameter.ParameterName = "$id";
        userIdParameter.Value = user.Id;
        cmd.Parameters.Add(userIdParameter);
        
        cmd.ExecuteNonQuery();
    }

    private void ResetLoginAttempts(System.Data.IDbConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Uzytkownicy SET liczba_blednych_logowan = 0, blokada_do = NULL WHERE id = $id";
        var userIdParameter = cmd.CreateParameter();
        userIdParameter.ParameterName = "$id";
        userIdParameter.Value = userId;
        cmd.Parameters.Add(userIdParameter);
        cmd.ExecuteNonQuery();
    }

    private List<string> GetUserRoles(System.Data.IDbConnection conn, long userId)
    {
        var roles = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT p.Nazwa FROM Uprawnienia p JOIN Uzytkownik_Uprawnienia uu ON p.Id = uu.uprawnienie_id WHERE uu.uzytkownik_id = $id";
        var userIdParameter = cmd.CreateParameter();
        userIdParameter.ParameterName = "$id";
        userIdParameter.Value = userId;
        cmd.Parameters.Add(userIdParameter);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) roles.Add(reader.GetString(0));
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

    public int Status { get; set; }
}
