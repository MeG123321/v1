namespace Magazyn.Migrations;

/// <summary>
/// Migracja: poprawia strukturę tabeli Uzytkownicy –
/// upewnia się, że klucze obce działają i dodaje indeksy.
/// Data: 04.04.2026
/// </summary>
public class Migration_20260404163254_PoprawaStrukturyUzytkownika : IMigration
{
    public string Id => "20260404163254_PoprawaStrukturyUzytkownika";

    public string Up() => @"
-- Indeksy przyspieszające wyszukiwanie
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_username      ON Uzytkownicy (LOWER(username));
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_email         ON Uzytkownicy (LOWER(Email));
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_pesel         ON Uzytkownicy (pesel);
CREATE INDEX IF NOT EXISTS IX_Uzytkownicy_zapomniany    ON Uzytkownicy (czy_zapomniany);
CREATE INDEX IF NOT EXISTS IX_UzytUprawnienia_uzyt      ON Uzytkownik_Uprawnienia (uzytkownik_id);
CREATE INDEX IF NOT EXISTS IX_UzytUprawnienia_uprawn    ON Uzytkownik_Uprawnienia (uprawnienie_id);

-- Włączenie kluczy obcych (na wypadek gdyby nie były włączone)
PRAGMA foreign_keys = ON;
";

    public string Down() => @"
DROP INDEX IF EXISTS IX_Uzytkownicy_username;
DROP INDEX IF EXISTS IX_Uzytkownicy_email;
DROP INDEX IF EXISTS IX_Uzytkownicy_pesel;
DROP INDEX IF EXISTS IX_Uzytkownicy_zapomniany;
DROP INDEX IF EXISTS IX_UzytUprawnienia_uzyt;
DROP INDEX IF EXISTS IX_UzytUprawnienia_uprawn;
";
}
