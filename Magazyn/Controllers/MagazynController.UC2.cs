using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;

namespace Magazyn.Controllers;

public partial class MagazynController : Controller
{
    [HttpGet]
    [Authorize(Roles = "Administrator,Kierownik magazynu,Pracownik magazynu")]
    public IActionResult StanyMagazynowe(string? nazwaTowar = null, long? rodzajId = null, string? imiePracownika = null, string? dataStanu = null)
    {
        if (!System.IO.File.Exists(DbPath))
            return View(new StanyMagazynoweVm());

        using var conn = Db.OpenConnection(DbPath);
        var vm = new StanyMagazynoweVm
        {
            NazwaTowaru = nazwaTowar,
            RodzajId = rodzajId,
            ImiePracownika = imiePracownika,
            DataStanu = dataStanu,
            Rodzaje = GetRodzaje(conn),
            Searched = Request.Query.ContainsKey("nazwaTowar") || Request.Query.ContainsKey("rodzajId") ||
                       Request.Query.ContainsKey("imiePracownika") || Request.Query.ContainsKey("dataStanu")
        };

        bool canSeeHistorical = User.IsInRole("Administrator") || User.IsInRole("Kierownik magazynu");
        bool useHistorical = canSeeHistorical && !string.IsNullOrWhiteSpace(dataStanu);

        var sql = new System.Text.StringBuilder();
        if (useHistorical)
        {
            sql.Append(@"
SELECT t.Id as TowarId, t.NazwaTowaru, r.Nazwa as RodzajTowaru, jm.Nazwa as JednostkaMiary,
       COALESCE((SELECT SUM(rt2.Ilosc) FROM RejestracjeTowaru rt2
                  WHERE rt2.TowarId = t.Id AND rt2.DataRejestracji <= $dataStanu || ' 23:59:59'), 0) as StanMagazynowy
FROM Towary t
JOIN TowarRodzaje r ON r.Id = t.RodzajId
JOIN JednostkiMiary jm ON jm.Id = t.JednostkaMiaryId
WHERE t.CzyAktywny = 1
  AND EXISTS (SELECT 1 FROM RejestracjeTowaru rx WHERE rx.TowarId = t.Id AND rx.DataRejestracji <= $dataStanu || ' 23:59:59')
");
        }
        else
        {
            sql.Append(@"
SELECT t.Id as TowarId, t.NazwaTowaru, r.Nazwa as RodzajTowaru, jm.Nazwa as JednostkaMiary,
       t.AktualnaIlosc as StanMagazynowy
FROM Towary t
JOIN TowarRodzaje r ON r.Id = t.RodzajId
JOIN JednostkiMiary jm ON jm.Id = t.JednostkaMiaryId
WHERE t.CzyAktywny = 1
");
        }

        if (!string.IsNullOrWhiteSpace(nazwaTowar))
            sql.Append("  AND LOWER(TRIM(t.NazwaTowaru)) LIKE '%' || LOWER(TRIM($nazwa)) || '%'\n");
        if (rodzajId.HasValue && rodzajId > 0)
            sql.Append("  AND t.RodzajId = $rodzajId\n");
        if (!string.IsNullOrWhiteSpace(imiePracownika))
            sql.Append(@"  AND EXISTS (SELECT 1 FROM RejestracjeTowaru rt3
                JOIN Uzytkownicy u3 ON u3.id = rt3.RejestrujacyUserId
                WHERE rt3.TowarId = t.Id AND LOWER(u3.firstName || ' ' || u3.LastName) LIKE '%' || LOWER($imie) || '%')
");
        sql.Append("ORDER BY t.NazwaTowaru");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql.ToString();
        if (!string.IsNullOrWhiteSpace(nazwaTowar)) cmd.Parameters.AddWithValue("$nazwa", nazwaTowar);
        if (rodzajId.HasValue && rodzajId > 0) cmd.Parameters.AddWithValue("$rodzajId", rodzajId.Value);
        if (!string.IsNullOrWhiteSpace(imiePracownika)) cmd.Parameters.AddWithValue("$imie", imiePracownika);
        if (useHistorical) cmd.Parameters.AddWithValue("$dataStanu", dataStanu!);

        using var dr = cmd.ExecuteReader();
        while (dr.Read())
        {
            vm.Wyniki.Add(new TowarStanDto
            {
                TowarId = Convert.ToInt64(dr["TowarId"]),
                NazwaTowaru = dr["NazwaTowaru"].ToString()!,
                RodzajTowaru = dr["RodzajTowaru"].ToString()!,
                JednostkaMiary = dr["JednostkaMiary"].ToString()!,
                StanMagazynowy = Convert.ToDecimal(dr["StanMagazynowy"])
            });
        }

        return View(vm);
    }
}
