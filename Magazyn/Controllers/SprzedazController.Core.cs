using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using System.Linq;
using System.Security.Claims;

namespace Magazyn.Controllers;

[Authorize]
public partial class SprzedazController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SprzedazController> _logger;

    public SprzedazController(IWebHostEnvironment env, ILogger<SprzedazController> logger)
    {
        _env = env;
        _logger = logger;
    }

    private string DbPath => Db.GetDbPath(_env);
    private static string SL(string? value) => (value ?? "").Replace('\r', '_').Replace('\n', '_');

    private long GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(claimValue) || !long.TryParse(claimValue, out var userId))
            throw new InvalidOperationException("Brak identyfikatora użytkownika w sesji.");

        return userId;
    }

    private List<SprzedazPozycjaVm> GetDostepneTowary(System.Data.IDbConnection conn)
    {
        var list = new List<SprzedazPozycjaVm>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT t.Id, t.NazwaTowaru, jm.Nazwa AS JednostkaMiary, t.AktualnaIlosc
FROM Towary t
JOIN JednostkiMiary jm ON jm.Id = t.JednostkaMiaryId
WHERE t.CzyAktywny = 1 AND t.AktualnaIlosc > 0
ORDER BY t.NazwaTowaru";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SprzedazPozycjaVm
            {
                TowarId = Convert.ToInt64(reader["Id"]),
                NazwaTowaru = reader["NazwaTowaru"].ToString()!,
                JednostkaMiary = reader["JednostkaMiary"].ToString()!,
                DostepnaIlosc = Convert.ToDecimal(reader["AktualnaIlosc"])
            });
        }

        return list;
    }

    private void ReloadPozycje(RejestracjaSprzedazyVm vm, System.Data.IDbConnection conn)
    {
        vm.Pozycje ??= new List<SprzedazPozycjaVm>();
        var existing = vm.Pozycje.ToDictionary(p => p.TowarId, p => p.Ilosc);
        var list = GetDostepneTowary(conn);
        foreach (var item in list)
        {
            if (existing.TryGetValue(item.TowarId, out var ilosc))
                item.Ilosc = ilosc;
        }

        vm.Pozycje = list;
    }
}
