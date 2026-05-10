using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Magazyn.Data;

public static class Db
{
    private static readonly object _repairLock = new();

    public static string GetDbPath(IWebHostEnvironment env) =>
        Path.Combine(env.WebRootPath, "magazyn.db");

    public static SqliteConnection OpenConnection(string dbPath)
    {
        EnsureDatabaseExists(dbPath);

        var connection = new SqliteConnection($"Data Source={dbPath}");
        try
        {
            connection.Open();

            if (!IsIntegrityOk(connection))
            {
                connection.Dispose();
                return OpenRepairedConnection(dbPath);
            }

            return connection;
        }
        catch (SqliteException ex) when (IsMalformedDatabase(ex))
        {
            connection.Dispose();
            return OpenRepairedConnection(dbPath);
        }
    }

    private static void EnsureDatabaseExists(string dbPath)
    {
        if (File.Exists(dbPath)) return;
        ReplaceDatabaseFromTemplate(dbPath);
    }

    private static bool IsIntegrityOk(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = cmd.ExecuteScalar()?.ToString()?.Trim();
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static bool IsMalformedDatabase(SqliteException ex)
        => ex.SqliteErrorCode == 11 || ex.Message.Contains("database disk image is malformed", StringComparison.OrdinalIgnoreCase);

    private static void RepairDatabase(string dbPath)
    {
        lock (_repairLock)
        {
            if (IsDatabaseHealthy(dbPath)) return;

            var backupPath = GetCorruptBackupPath(dbPath);
            if (File.Exists(dbPath))
            {
                File.Move(dbPath, backupPath, overwrite: true);
            }

            ReplaceDatabaseFromTemplate(dbPath);
        }
    }

    private static SqliteConnection OpenRepairedConnection(string dbPath)
    {
        RepairDatabase(dbPath);
        var repairedConnection = new SqliteConnection($"Data Source={dbPath}");
        repairedConnection.Open();
        return repairedConnection;
    }

    private static bool IsDatabaseHealthy(string dbPath)
    {
        if (!File.Exists(dbPath)) return false;

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            return IsIntegrityOk(connection);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static void ReplaceDatabaseFromTemplate(string dbPath)
    {
        var templatePath = GetTemplatePath(dbPath);
        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException($"Brak pliku szablonu bazy danych: {templatePath}");
        }

        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        File.Copy(templatePath, dbPath, overwrite: true);
    }

    private static string GetTemplatePath(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath) ?? string.Empty;
        return Path.Combine(directory, "magazyn.template.db");
    }

    private static string GetCorruptBackupPath(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath) ?? string.Empty;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(directory, $"magazyn.corrupt-{timestamp}.db");
    }
}
