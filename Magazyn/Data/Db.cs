using Microsoft.Data.Sqlite;

namespace Magazyn.Data;

/// <summary>
/// Pomocnicza klasa statyczna do obsługi połączenia z bazą danych SQLite.
/// </summary>
public static class Db
{
    /// <summary>
    /// Zwraca pełną ścieżkę do pliku bazy danych znajdującego się w katalogu wwwroot.
    /// </summary>
    /// <param name="env">Środowisko hosta aplikacji (dostarcza ścieżkę do wwwroot).</param>
    /// <returns>Bezwzględna ścieżka do pliku magazyn.db.</returns>
    public static string GetDbPath(IWebHostEnvironment env) =>
        Path.Combine(env.WebRootPath, "magazyn.db");

    /// <summary>
    /// Otwiera i zwraca gotowe do użycia połączenie z bazą danych SQLite.
    /// Wywołujący jest odpowiedzialny za zamknięcie/dispose połączenia.
    /// </summary>
    /// <param name="dbPath">Ścieżka do pliku bazy danych.</param>
    /// <returns>Otwarte połączenie <see cref="SqliteConnection"/>.</returns>
    public static SqliteConnection OpenConnection(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }
}
