using Microsoft.Data.Sqlite;

namespace Magazyn.Data;

public static class DbInit
{
    public static void EnsureTables(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS TowarRodzaje (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nazwa TEXT NOT NULL UNIQUE,
  CzyAktywny INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS JednostkiMiary (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nazwa TEXT NOT NULL UNIQUE,
  Skrot TEXT,
  CzyAktywny INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS StawkiVat (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nazwa TEXT NOT NULL,
  Wartosc REAL NOT NULL,
  CzyAktywny INTEGER NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_StawkiVat_Wartosc ON StawkiVat(Wartosc);

CREATE TABLE IF NOT EXISTS Towary (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  NazwaTowaru TEXT NOT NULL,
  RodzajId INTEGER NOT NULL REFERENCES TowarRodzaje(Id),
  JednostkaMiaryId INTEGER NOT NULL REFERENCES JednostkiMiary(Id),
  AktualnaIlosc REAL NOT NULL DEFAULT 0,
  CzyAktywny INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS RejestracjeTowaru (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  TowarId INTEGER NOT NULL REFERENCES Towary(Id),
  Ilosc REAL NOT NULL,
  CenaNetto REAL NOT NULL,
  StawkaVatId INTEGER NOT NULL REFERENCES StawkiVat(Id),
  Opis TEXT,
  Dostawca TEXT,
  DataDostawy TEXT,
  DataRejestracji TEXT NOT NULL DEFAULT (datetime('now')),
  RejestrujacyUserId INTEGER NOT NULL REFERENCES Uzytkownicy(id)
);

CREATE TABLE IF NOT EXISTS PlanowaneZmianyVat (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Zakres TEXT NOT NULL,
  TowarId INTEGER REFERENCES Towary(Id),
  RodzajId INTEGER REFERENCES TowarRodzaje(Id),
  NowaStawkaVatId INTEGER NOT NULL REFERENCES StawkiVat(Id),
  DataObowiazywania TEXT NOT NULL,
  CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
  CreatedByUserId INTEGER NOT NULL REFERENCES Uzytkownicy(id)
);
";
        cmd.ExecuteNonQuery();

        // Seed initial data if tables are empty
        using var seedCmd = conn.CreateCommand();
        seedCmd.CommandText = @"
INSERT OR IGNORE INTO JednostkiMiary (Nazwa, Skrot) VALUES ('sztuka','szt'),('kilogram','kg'),('litr','l'),('metr','m'),('opakowanie','op');
INSERT OR IGNORE INTO StawkiVat (Nazwa, Wartosc) VALUES ('0%',0.00),('5%',0.05),('8%',0.08),('23%',0.23);
INSERT OR IGNORE INTO TowarRodzaje (Nazwa) VALUES ('Elektronika'),('Materiały budowlane'),('Artykuły spożywcze'),('Chemikalia'),('Inne');
";
        seedCmd.ExecuteNonQuery();
    }
}
