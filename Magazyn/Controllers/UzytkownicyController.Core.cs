using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;

namespace Magazyn.Controllers;

[Authorize]
public partial class UzytkownicyController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UzytkownicyController> _logger;

    public UzytkownicyController(IWebHostEnvironment env, ILogger<UzytkownicyController> logger)
    {
        _env = env;
        _logger = logger;
    }

    private string DbPath => Db.GetDbPath(_env);

    private static string SL(string? value) =>
        (value ?? "").Replace('\r', '_').Replace('\n', '_');

    private static int PlecToInt(string? genderText)
    {
        if (string.IsNullOrWhiteSpace(genderText)) return 0;
        return genderText.Trim().Equals("Mężczyzna", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static int StatusToInt(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText)) return 1;
        return statusText.Trim().Equals("Aktywny", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static string PlecToText(int genderValue) => genderValue == 1 ? "Mężczyzna" : "Kobieta";

    private static string StatusToText(object dbValue)
    {
        if (dbValue == DBNull.Value) return "Nieaktywny";
        return Convert.ToInt32(dbValue) == 1 ? "Aktywny" : "Nieaktywny";
    }
}