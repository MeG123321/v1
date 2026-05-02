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

        string nazwaTowar = "";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            var val = cmd.ExecuteScalar();
            if (val == null || val is DBNull) return RedirectToAction(nameof(StanyMagazynowe));
            nazwaTowar = val.ToString()!;
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

        var historia = new List<HistoriaWpisDto>();
        using (var dr = histCmd.ExecuteReader())
        {
            while (dr.Read())
            {
                historia.Add(new HistoriaWpisDto
                {
                    Id = Convert.ToInt64(dr["Id"]),
                    DataRejestracji = dr["DataRejestracji"].ToString()!,
                    ImieNazwisko = dr["ImieNazwisko"].ToString()!,
                    Ilosc = Convert.ToDecimal(dr["Ilosc"])
                });
            }
        }

        var pracownicy = new List<PracownikListDto>();
        using (var pCmd = conn.CreateCommand())
        {
            pCmd.CommandText = @"
SELECT DISTINCT u.id, u.firstName || ' ' || u.LastName AS ImieNazwisko
FROM RejestracjeTowaru rt
JOIN Uzytkownicy u ON u.id = rt.RejestrujacyUserId
WHERE rt.TowarId = $towarId
ORDER BY ImieNazwisko";
            pCmd.Parameters.AddWithValue("$towarId", id);
            using var pr = pCmd.ExecuteReader();
            while (pr.Read())
                pracownicy.Add(new PracownikListDto { Id = Convert.ToInt64(pr["id"]), ImieNazwisko = pr["ImieNazwisko"].ToString()! });
        }

        var vm = new HistoriaStanowVm
        {
            TowarId = id,
            NazwaTowaru = nazwaTowar,
            DataOd = dataOd,
            DataDo = dataDo,
            PracownikId = pracownikId,
            Historia = historia,
            Pracownicy = pracownicy,
            Filtered = !string.IsNullOrWhiteSpace(dataOd) || !string.IsNullOrWhiteSpace(dataDo) || (pracownikId.HasValue && pracownikId > 0)
        };

        return View(vm);
    }
}
