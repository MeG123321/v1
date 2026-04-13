namespace Magazyn.Migrations;

/// <summary>
/// Migracja: zmiana kolumny Plec na wartość ENUM-like (INTEGER 0=Kobieta, 1=Mężczyzna).
/// Dodaje sprawdzenie poprawności wartości przez constraint.
/// Data: 04.04.2026
/// </summary>
public class Migration_20260404132408_ZmianaPlciNaEnum : IMigration
{
    public string Id => "20260404132408_ZmianaPlciNaEnum";

    public string Up() => @"
-- SQLite nie obsługuje ALTER COLUMN, więc tworzymy nową tabelę i przenosimy dane.
-- Kolumna Plec: 0 = Kobieta, 1 = Mężczyzna (CHECK constraint)

PRAGMA foreign_keys = OFF;

CREATE TABLE IF NOT EXISTS Uzytkownicy_new (
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

INSERT INTO Uzytkownicy_new SELECT * FROM Uzytkownicy;

DROP TABLE Uzytkownicy;
ALTER TABLE Uzytkownicy_new RENAME TO Uzytkownicy;

PRAGMA foreign_keys = ON;
";

    public string Down() => @"
-- Cofnięcie: usunięcie CHECK constraints (przywracamy oryginalną definicję)
PRAGMA foreign_keys = OFF;

CREATE TABLE IF NOT EXISTS Uzytkownicy_old (
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

INSERT INTO Uzytkownicy_old SELECT * FROM Uzytkownicy;
DROP TABLE Uzytkownicy;
ALTER TABLE Uzytkownicy_old RENAME TO Uzytkownicy;

PRAGMA foreign_keys = ON;
";
}
