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

    private List<TowarRodzajDto> GetRodzaje(System.Data.IDbConnection connection)
    {
        var rodzaje = new List<TowarRodzajDto>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Nazwa FROM TowarRodzaje WHERE CzyAktywny = 1 ORDER BY Nazwa";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            rodzaje.Add(new TowarRodzajDto { Id = Convert.ToInt64(reader["Id"]), Nazwa = reader["Nazwa"].ToString()! });
        return rodzaje;
    }

    private List<JednostkaMiaryDto> GetJednostkiMiary(System.Data.IDbConnection connection)
    {
        var jednostkiMiary = new List<JednostkaMiaryDto>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Nazwa, Skrot FROM JednostkiMiary WHERE CzyAktywny = 1 ORDER BY Nazwa";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            jednostkiMiary.Add(new JednostkaMiaryDto { Id = Convert.ToInt64(reader["Id"]), Nazwa = reader["Nazwa"].ToString()!, Skrot = reader["Skrot"] is DBNull ? null : reader["Skrot"].ToString() });
        return jednostkiMiary;
    }

    private List<StawkaVatDto> GetStawkiVat(System.Data.IDbConnection connection)
    {
        var stawkiVat = new List<StawkaVatDto>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Nazwa, Wartosc FROM StawkiVat WHERE CzyAktywny = 1 ORDER BY Wartosc";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            stawkiVat.Add(new StawkaVatDto { Id = Convert.ToInt64(reader["Id"]), Nazwa = reader["Nazwa"].ToString()!, Wartosc = Convert.ToDouble(reader["Wartosc"]) });
        return stawkiVat;
    }
}
