using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using System.Text;

namespace Magazyn.Controllers;

public partial class SprzedazController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik sprzedazy")]
    public IActionResult HistoriaSprzedazy(string? dataOd = null, string? dataDo = null, string? nabywca = null, string? sprzedawca = null, string? towar = null)
    {
        var vm = new HistoriaSprzedazyVm
        {
            DataOd = dataOd,
            DataDo = dataDo,
            Nabywca = nabywca,
            Sprzedawca = sprzedawca,
            Towar = towar,
            Filtered = !string.IsNullOrWhiteSpace(dataOd) || !string.IsNullOrWhiteSpace(dataDo) ||
                       !string.IsNullOrWhiteSpace(nabywca) || !string.IsNullOrWhiteSpace(sprzedawca) ||
                       !string.IsNullOrWhiteSpace(towar)
        };

        if (!System.IO.File.Exists(DbPath)) return View(vm);

        if (!ValidateDateRange(dataOd, dataDo, out var validationMessage))
        {
            vm.ErrorMessage = validationMessage;
            return View(vm);
        }

        using var conn = Db.OpenConnection(DbPath);
        var sql = new StringBuilder(@"
SELECT s.Id,
       s.DataSprzedazy,
       s.Nabywca,
       u.FirstName || ' ' || u.LastName AS Sprzedawca,
       (SELECT COUNT(*) FROM SprzedazPozycje sp WHERE sp.SprzedazId = s.Id) AS LiczbaPozycji
FROM Sprzedaze s
JOIN Uzytkownicy u ON u.id = s.SprzedawcaUserId
WHERE 1 = 1
");

        if (!string.IsNullOrWhiteSpace(dataOd))
            sql.Append("  AND s.DataSprzedazy >= $dataOd\n");
        if (!string.IsNullOrWhiteSpace(dataDo))
            sql.Append("  AND s.DataSprzedazy <= $dataDo\n");
        if (!string.IsNullOrWhiteSpace(nabywca))
            sql.Append("  AND LOWER(TRIM(s.Nabywca)) LIKE '%' || LOWER(TRIM($nabywca)) || '%'\n");
        if (!string.IsNullOrWhiteSpace(sprzedawca))
            sql.Append("  AND LOWER(u.FirstName || ' ' || u.LastName) LIKE '%' || LOWER(TRIM($sprzedawca)) || '%'\n");
        if (!string.IsNullOrWhiteSpace(towar))
        {
            sql.Append(@"  AND EXISTS (
        SELECT 1
        FROM SprzedazPozycje sp
        JOIN Towary t ON t.Id = sp.TowarId
        WHERE sp.SprzedazId = s.Id
          AND LOWER(TRIM(t.NazwaTowaru)) LIKE '%' || LOWER(TRIM($towar)) || '%'
    )
");
        }
        sql.Append("ORDER BY s.DataSprzedazy DESC, s.Id DESC");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql.ToString();
        if (!string.IsNullOrWhiteSpace(dataOd)) cmd.Parameters.AddWithValue("$dataOd", dataOd);
        if (!string.IsNullOrWhiteSpace(dataDo)) cmd.Parameters.AddWithValue("$dataDo", dataDo);
        if (!string.IsNullOrWhiteSpace(nabywca)) cmd.Parameters.AddWithValue("$nabywca", nabywca);
        if (!string.IsNullOrWhiteSpace(sprzedawca)) cmd.Parameters.AddWithValue("$sprzedawca", sprzedawca);
        if (!string.IsNullOrWhiteSpace(towar)) cmd.Parameters.AddWithValue("$towar", towar);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            vm.Wyniki.Add(new SprzedazHistoriaRowDto
            {
                Id = Convert.ToInt64(reader["Id"]),
                DataSprzedazy = reader["DataSprzedazy"].ToString()!,
                Nabywca = reader["Nabywca"].ToString()!,
                Sprzedawca = reader["Sprzedawca"].ToString()!,
                LiczbaPozycji = Convert.ToInt32(reader["LiczbaPozycji"])
            });
        }

        return View(vm);
    }

    private static bool ValidateDateRange(string? dataOd, string? dataDo, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(dataOd) && string.IsNullOrWhiteSpace(dataDo)) return true;

        if (!string.IsNullOrWhiteSpace(dataOd) && !DateTime.TryParse(dataOd, out var od))
        {
            message = "Niepoprawny zakres dat.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dataDo) && !DateTime.TryParse(dataDo, out var doDate))
        {
            message = "Niepoprawny zakres dat.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dataOd) && !string.IsNullOrWhiteSpace(dataDo))
        {
            DateTime.TryParse(dataOd, out var odDate);
            DateTime.TryParse(dataDo, out var doParsed);
            if (doParsed.Date < odDate.Date)
            {
                message = "Niepoprawny zakres dat.";
                return false;
            }
        }

        return true;
    }
}
