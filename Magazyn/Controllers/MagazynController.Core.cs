using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Magazyn.Data;
using Magazyn.Models;
using System.Security.Claims;

namespace Magazyn.Controllers;

[Authorize]
public partial class MagazynController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MagazynController> _logger;

    public MagazynController(IWebHostEnvironment env, ILogger<MagazynController> logger)
    {
        _env = env;
        _logger = logger;
    }

    private string DbPath => Db.GetDbPath(_env);
    private static string SL(string? value) => (value ?? "").Replace('\r', '_').Replace('\n', '_');

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private List<TowarRodzajDto> GetRodzaje(System.Data.IDbConnection conn)
    {
        var list = new List<TowarRodzajDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nazwa FROM TowarRodzaje WHERE CzyAktywny = 1 ORDER BY Nazwa";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TowarRodzajDto { Id = Convert.ToInt64(r["Id"]), Nazwa = r["Nazwa"].ToString()! });
        return list;
    }

    private List<JednostkaMiaryDto> GetJednostkiMiary(System.Data.IDbConnection conn)
    {
        var list = new List<JednostkaMiaryDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nazwa, Skrot FROM JednostkiMiary WHERE CzyAktywny = 1 ORDER BY Nazwa";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new JednostkaMiaryDto { Id = Convert.ToInt64(r["Id"]), Nazwa = r["Nazwa"].ToString()!, Skrot = r["Skrot"] is DBNull ? null : r["Skrot"].ToString() });
        return list;
    }

    private List<StawkaVatDto> GetStawkiVat(System.Data.IDbConnection conn)
    {
        var list = new List<StawkaVatDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nazwa, Wartosc FROM StawkiVat WHERE CzyAktywny = 1 ORDER BY Wartosc";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new StawkaVatDto { Id = Convert.ToInt64(r["Id"]), Nazwa = r["Nazwa"].ToString()!, Wartosc = Convert.ToDouble(r["Wartosc"]) });
        return list;
    }
}
