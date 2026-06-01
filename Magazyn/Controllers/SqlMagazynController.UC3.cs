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

       using var connection = Db.OpenConnection(DbPath);

       string nazwaTowaru = "";
       using (var command = connection.CreateCommand())
        {
           command.CommandText = "SELECT NazwaTowaru FROM Towary WHERE Id = $id";
           command.Parameters.AddWithValue("$id", id);
           var val = command.ExecuteScalar();
            if (val == null || val is DBNull) return RedirectToAction(nameof(StanyMagazynowe));
           nazwaTowaru = val.ToString()!;
        }

       var queryBuilder = new System.Text.StringBuilder(@"
SELECT rt.Id, rt.DataRejestracji, u.firstName || ' ' || u.LastName AS ImieNazwisko, rt.Ilosc
FROM RejestracjeTowaru rt
JOIN Uzytkownicy u ON u.id = rt.RejestrujacyUserId
WHERE rt.TowarId = $towarId
");
       if (!string.IsNullOrWhiteSpace(dataOd)) queryBuilder.Append("  AND rt.DataRejestracji >= $dataOd\n");
       if (!string.IsNullOrWhiteSpace(dataDo)) queryBuilder.Append("  AND rt.DataRejestracji <= $dataDo || ' 23:59:59'\n");
       if (pracownikId.HasValue && pracownikId > 0) queryBuilder.Append("  AND rt.RejestrujacyUserId = $pracownikId\n");
       queryBuilder.Append("ORDER BY rt.DataRejestracji DESC");

       using var historyCommand = connection.CreateCommand();
       historyCommand.CommandText = queryBuilder.ToString();
       historyCommand.Parameters.AddWithValue("$towarId", id);
       if (!string.IsNullOrWhiteSpace(dataOd)) historyCommand.Parameters.AddWithValue("$dataOd", dataOd);
       if (!string.IsNullOrWhiteSpace(dataDo)) historyCommand.Parameters.AddWithValue("$dataDo", dataDo);
       if (pracownikId.HasValue && pracownikId > 0) historyCommand.Parameters.AddWithValue("$pracownikId", pracownikId.Value);

        var historyEntries = new List<HistoriaWpisDto>();
       using (var historyReader = historyCommand.ExecuteReader())
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
       using (var employeeCommand = connection.CreateCommand())
        {
           employeeCommand.CommandText = @"
SELECT DISTINCT u.id, u.firstName || ' ' || u.LastName AS ImieNazwisko
FROM RejestracjeTowaru rt
JOIN Uzytkownicy u ON u.id = rt.RejestrujacyUserId
WHERE rt.TowarId = $towarId
ORDER BY ImieNazwisko";
           employeeCommand.Parameters.AddWithValue("$towarId", id);
           using var employeeReader = employeeCommand.ExecuteReader();
            while (employeeReader.Read())
                employeeOptions.Add(new PracownikListDto { Id = Convert.ToInt64(employeeReader["id"]), ImieNazwisko = employeeReader["ImieNazwisko"].ToString()! });
        }

       var viewModel = new HistoriaStanowVm
        {
            TowarId = id,
           NazwaTowaru = nazwaTowaru,
            DataOd = dataOd,
            DataDo = dataDo,
            PracownikId = pracownikId,
            Historia = historyEntries,
            Pracownicy = employeeOptions,
            Filtered = !string.IsNullOrWhiteSpace(dataOd) || !string.IsNullOrWhiteSpace(dataDo) || (pracownikId.HasValue && pracownikId > 0)
        };

       return View(viewModel);
    }
}
