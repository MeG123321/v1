using Microsoft.Data.Sqlite;

namespace Magazyn.Migrations;

/// <summary>
/// Snapshot aktualnego modelu bazy danych (stan po wszystkich migracjach).
/// Zawiera pełną definicję schematu SQLite dla aplikacji Magazyn GiTA.
/// </summary>
public static class MagazynDbContextModelSnapshot
{
    /// <summary>
    /// Pełny schemat bazy danych – używany do inicjalizacji nowej bazy od zera.
    /// </summary>
    public static readonly string FullSchema = @"
PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;

-- ==================================================
-- Tabela: Uprawnienia (role użytkowników)
-- ==================================================
CREATE TABLE IF NOT EXISTS Uprawnienia (
    Id    INTEGER PRIMARY KEY AUTOINCREMENT,
    Nazwa TEXT    NOT NULL UNIQUE
);

INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Administrator');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Kierownik magazynu');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Kierownik sprzedazy');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Sprzedawca');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Pracownik magazynu');

-- ==================================================
-- Tabela: Uzytkownicy
-- ==================================================
CREATE TABLE IF NOT EXISTS Uzytkownicy (
    id                       INTEGER PRIMARY KEY AUTOINCREMENT,
    username                 TEXT    NOT NULL UNIQUE,
    Password                 TEXT,
    firstName                TEXT    NOT NULL,
    LastName                 TEXT    NOT NULL,
    pesel                    TEXT    UNIQUE,
    Status                   INTEGER NOT NULL DEFAULT 1  CHECK(Status IN (0, 1)),
    Plec                     INTEGER NOT NULL DEFAULT 0  CHECK(Plec   IN (0, 1)),
    DataUrodzenia            TEXT,
    Email                    TEXT    UNIQUE,
    NrTelefonu               TEXT,
    Miejscowosc              TEXT,
    KodPocztowy              TEXT,
    numer_posesji            TEXT,
    Ulica                    TEXT,
    NrLokalu                 TEXT,
    blokada_do               TEXT,
    czy_zapomniany           INTEGER NOT NULL DEFAULT 0  CHECK(czy_zapomniany IN (0, 1)),
    DataZapomnienia          TEXT,
    ZapomnialUserId          INTEGER,
    liczba_blednych_logowan  INTEGER NOT NULL DEFAULT 0
);

-- ==================================================
-- Tabela: Uzytkownik_Uprawnienia (relacja wiele-do-wielu)
-- ==================================================
CREATE TABLE IF NOT EXISTS Uzytkownik_Uprawnienia (
    uzytkownik_id  INTEGER NOT NULL REFERENCES Uzytkownicy(id)  ON DELETE CASCADE,
    uprawnienie_id INTEGER NOT NULL REFERENCES Uprawnienia(Id)  ON DELETE CASCADE,
    PRIMARY KEY (uzytkownik_id, uprawnienie_id)
);

-- ==================================================
-- Indeksy
-- ==================================================
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_username   ON Uzytkownicy (LOWER(username));
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_email      ON Uzytkownicy (LOWER(Email));
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_pesel      ON Uzytkownicy (pesel);
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_zapomniany ON Uzytkownicy (czy_zapomniany);
CREATE INDEX IF NOT EXISTS IX_UzytUprawnienia_uzyt   ON Uzytkownik_Uprawnienia (uzytkownik_id);
CREATE INDEX IF NOT EXISTS IX_UzytUprawnienia_uprawn ON Uzytkownik_Uprawnienia (uprawnienie_id);

-- ==================================================
-- Tabela śledzenia migracji
-- ==================================================
CREATE TABLE IF NOT EXISTS __MigrationHistory (
    MigrationId  TEXT PRIMARY KEY,
    AppliedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);
";

    /// <summary>
    /// Inicjalizuje bazę danych od zera (jeśli nie istnieje) lub stosuje brakujące migracje.
    /// </summary>
    public static void EnsureCreated(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = FullSchema;
        cmd.ExecuteNonQuery();

        // Zarejestruj wszystkie migracje jako zastosowane (schema już istnieje)
        var migrations = new IMigration[]
        {
            new Migration_20260313123240_InitialCreate(),
            new Migration_20260403081815_SeedMissingRole(),
            new Migration_20260404132408_ZmianaPlciNaEnum(),
            new Migration_20260404163254_PoprawaStrukturyUzytkownika(),
        };

        foreach (var migration in migrations)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"INSERT OR IGNORE INTO __MigrationHistory (MigrationId) VALUES ($id);";
            insertCmd.Parameters.AddWithValue("$id", migration.Id);
            insertCmd.ExecuteNonQuery();
        }
    }
}
