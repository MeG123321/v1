using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class MagazynController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu")]
    public IActionResult HistoriaStanow(long id, string? dataOd = null, string? dataDo = null, long? pracownikId = null)
    {
        if (!System.IO.File.Exists(DbPath))
            return View(new HistoriaStanowVm());

        using var conn = Db.OpenConnection(DbPath);

        string towarName = "";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            var val = cmd.ExecuteScalar();
            if (val == null || val is DBNull) return RedirectToAction(nameof(StanyMagazynowe));
            towarName = val.ToString()!;
        }

        var sql = new System.Text.StringBuilder(@"
SELECT rt.Id, rt.DataRejestracji, u.firstName || ' ' || u.LastName AS ImieNazwisko, rt.Ilosc
FROM RejestracjeTowaru rt
JOIN Uzytkownicy u ON u.id = rt.RejestrujacyUserId
WHERE rt.TowarId = $towarId
");
        if (!string.IsNullOrWhiteSpace(dataOd)) sql.Append("  AND rt.DataRejestracji >= $dataOd\n");
        if (!string.IsNullOrWhiteSpace(dataDo)) sql.Append("  AND rt.DataRejestracji <= $dataDo || ' 23:59:59'\n");
        if (pracownikId.HasValue && pracownikId > 0) sql.Append("  AND rt.RejestrujacyUserId = $pracownikId\n");
        sql.Append("ORDER BY rt.DataRejestracji DESC");

        using var histCmd = conn.CreateCommand();
        histCmd.CommandText = sql.ToString();
        histCmd.Parameters.AddWithValue("$towarId", id);
        if (!string.IsNullOrWhiteSpace(dataOd)) histCmd.Parameters.AddWithValue("$dataOd", dataOd);
        if (!string.IsNullOrWhiteSpace(dataDo)) histCmd.Parameters.AddWithValue("$dataDo", dataDo);
        if (pracownikId.HasValue && pracownikId > 0) histCmd.Parameters.AddWithValue("$pracownikId", pracownikId.Value);

        var historyEntries = new List<HistoriaWpisDto>();
        using (var historyReader = histCmd.ExecuteReader())
        {
            while (historyReader.Read())
            {
                historyEntries.Add(new HistoriaWpisDto
                {
                    Id = Convert.ToInt64(historyReader["Id"]),
                    DataRejestracji = historyReader["DataRejestracji"].ToString()!,
                    ImieNazwisko = historyReader["ImieNazwisko"].ToString()!,
                    Ilosc = Convert.ToDecimal(historyReader["Ilosc"])
                });
            }
        }

        var employeeOptions = new List<PracownikListDto>();
        using (var pCmd = conn.CreateCommand())
        {
            pCmd.CommandText = @"
SELECT DISTINCT u.id, u.firstName || ' ' || u.LastName AS ImieNazwisko
FROM RejestracjeTowaru rt
JOIN Uzytkownicy u ON u.id = rt.RejestrujacyUserId
WHERE rt.TowarId = $towarId
ORDER BY ImieNazwisko";
            pCmd.Parameters.AddWithValue("$towarId", id);
            using var employeeReader = pCmd.ExecuteReader();
            while (employeeReader.Read())
                employeeOptions.Add(new PracownikListDto { Id = Convert.ToInt64(employeeReader["id"]), ImieNazwisko = employeeReader["ImieNazwisko"].ToString()! });
        }

        var vm = new HistoriaStanowVm
        {
            TowarId = id,
            NazwaTowaru = towarName,
            DataOd = dataOd,
            DataDo = dataDo,
            PracownikId = pracownikId,
            Historia = historyEntries,
            Pracownicy = employeeOptions,
            Filtered = !string.IsNullOrWhiteSpace(dataOd) || !string.IsNullOrWhiteSpace(dataDo) || (pracownikId.HasValue && pracownikId > 0)
        };

        return View(vm);
    }
}
