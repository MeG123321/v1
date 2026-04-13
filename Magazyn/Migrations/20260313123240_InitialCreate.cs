namespace Magazyn.Migrations;

/// <summary>
/// Migracja początkowa: tworzy tabelę Uprawnienia, Uzytkownicy
/// oraz tabelę pośrednią Uzytkownik_Uprawnienia.
/// Data: 13.03.2026
/// </summary>
public class Migration_20260313123240_InitialCreate : IMigration
{
    public string Id => "20260313123240_InitialCreate";

    public string Up() => @"
-- Tabela uprawnień (ról)
CREATE TABLE IF NOT EXISTS Uprawnienia (
    Id    INTEGER PRIMARY KEY AUTOINCREMENT,
    Nazwa TEXT    NOT NULL UNIQUE
);

-- Podstawowe role systemowe
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Administrator');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Kierownik magazynu');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Kierownik sprzedazy');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Sprzedawca');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Pracownik magazynu');

-- Tabela użytkowników
CREATE TABLE IF NOT EXISTS Uzytkownicy (
    id                       INTEGER PRIMARY KEY AUTOINCREMENT,
    username                 TEXT    NOT NULL UNIQUE,
    Password                 TEXT,
    firstName                TEXT    NOT NULL,
    LastName                 TEXT    NOT NULL,
    pesel                    TEXT    UNIQUE,
    Status                   INTEGER NOT NULL DEFAULT 1,
    Plec                     INTEGER NOT NULL DEFAULT 0,
    DataUrodzenia            TEXT,
    Email                    TEXT    UNIQUE,
    NrTelefonu               TEXT,
    Miejscowosc              TEXT,
    KodPocztowy              TEXT,
    numer_posesji            TEXT,
    Ulica                    TEXT,
    NrLokalu                 TEXT,
    blokada_do               TEXT,
    czy_zapomniany           INTEGER NOT NULL DEFAULT 0,
    DataZapomnienia          TEXT,
    ZapomnialUserId          INTEGER,
    liczba_blednych_logowan  INTEGER NOT NULL DEFAULT 0
);

-- Tabela pośrednia: użytkownicy ↔ uprawnienia (wiele do wielu)
CREATE TABLE IF NOT EXISTS Uzytkownik_Uprawnienia (
    uzytkownik_id  INTEGER NOT NULL REFERENCES Uzytkownicy(id)  ON DELETE CASCADE,
    uprawnienie_id INTEGER NOT NULL REFERENCES Uprawnienia(Id)  ON DELETE CASCADE,
    PRIMARY KEY (uzytkownik_id, uprawnienie_id)
);
";

    public string Down() => @"
DROP TABLE IF EXISTS Uzytkownik_Uprawnienia;
DROP TABLE IF EXISTS Uzytkownicy;
DROP TABLE IF EXISTS Uprawnienia;
";
}
