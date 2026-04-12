using Microsoft.Data.Sqlite;

namespace Magazyn.Data;

public static class Db
{
    public static string GetDbPath(IWebHostEnvironment env) =>
        Path.Combine(env.WebRootPath, "magazyn.db");

    public static SqliteConnection OpenConnection(string dbPath)
    {
        var con = new SqliteConnection($"Data Source={dbPath}");
        con.Open();
        return con;
    }
}
