using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using System.Globalization;
using System.Linq;

namespace Magazyn.Controllers;

[Authorize]
public partial class UzytkownicyController : Controller
{
    private const int GenderFemale = 0;
    private const int GenderMale = 1;

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
        if (string.IsNullOrWhiteSpace(genderText)) return GenderFemale;
        return genderText.Trim().Equals("Mężczyzna", StringComparison.OrdinalIgnoreCase) ? GenderMale : GenderFemale;
    }

    private static int StatusToInt(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText)) return 1;
        return statusText.Trim().Equals("Aktywny", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static string PlecToText(int genderValue) => genderValue == GenderMale ? "Mężczyzna" : "Kobieta";

    private static string StatusToText(object dbValue)
    {
        if (dbValue == DBNull.Value) return "Nieaktywny";
        return Convert.ToInt32(dbValue) == 1 ? "Aktywny" : "Nieaktywny";
    }

    private static bool TryParsePesel(string? pesel, out DateOnly birthDate, out int gender)
    {
        birthDate = default;
        gender = GenderFemale;

        if (string.IsNullOrWhiteSpace(pesel) || pesel.Length != 11)
            return false;

        if (!pesel.All(char.IsDigit))
            return false;

        int year = (pesel[0] - '0') * 10 + (pesel[1] - '0');
        int month = (pesel[2] - '0') * 10 + (pesel[3] - '0');
        int day = (pesel[4] - '0') * 10 + (pesel[5] - '0');

        int century;
        if (month >= 1 && month <= 12)
        {
            century = 1900;
        }
        else if (month >= 21 && month <= 32)
        {
            century = 2000;
            month -= 20;
        }
        else if (month >= 41 && month <= 52)
        {
            century = 2100;
            month -= 40;
        }
        else if (month >= 61 && month <= 72)
        {
            century = 2200;
            month -= 60;
        }
        else if (month >= 81 && month <= 92)
        {
            century = 1800;
            month -= 80;
        }
        else
        {
            return false;
        }

        int fullYear = century + year;
        if (!DateOnly.TryParseExact($"{fullYear:0000}-{month:00}-{day:00}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDate))
            return false;

        int[] weights = { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            int digit = pesel[i] - '0';
            sum += digit * weights[i];
        }

        int checksum = (10 - (sum % 10)) % 10;
        if (checksum != pesel[10] - '0')
            return false;

        gender = ((pesel[9] - '0') % 2 == 1) ? GenderMale : GenderFemale;
        return true;
    }

    private static bool TryValidatePeselConsistency(string? pesel, DateOnly? dataUrodzenia, string? plec, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(pesel))
            return true;

        if (!TryParsePesel(pesel, out var peselBirthDate, out var peselGender))
        {
            error = "Nieprawidłowy numer PESEL.";
            return false;
        }

        bool mismatch = false;
        if (dataUrodzenia.HasValue && peselBirthDate != dataUrodzenia.Value)
            mismatch = true;

        if (!string.IsNullOrWhiteSpace(plec))
        {
            var plecTrimmed = plec.Trim();
            bool selectedMale = plecTrimmed.Equals("Mężczyzna", StringComparison.OrdinalIgnoreCase);
            bool selectedFemale = plecTrimmed.Equals("Kobieta", StringComparison.OrdinalIgnoreCase);
            bool peselMale = peselGender == GenderMale;

            if ((selectedMale && !peselMale) || (selectedFemale && peselMale))
                mismatch = true;
        }

        if (mismatch)
        {
            error = "PESEL nie zgadza się z płcią lub datą urodzenia.";
            return false;
        }

        return true;
    }
}
