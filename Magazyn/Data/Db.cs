using Microsoft.Data.Sqlite;

namespace Magazyn.Data;

public static class Db
{
    public static string GetDbPath(IWebHostEnvironment env) =>
        Path.Combine(env.WebRootPath, "magazyn.db");

    public static SqliteConnection OpenConnection(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }
}
