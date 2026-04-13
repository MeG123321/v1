namespace Magazyn.Migrations;

/// <summary>
/// Migracja: uzupełnia brakujące role jeśli nie zostały dodane w InitialCreate.
/// Data: 03.04.2026
/// </summary>
public class Migration_20260403081815_SeedMissingRole : IMigration
{
    public string Id => "20260403081815_SeedMissingRole";

    public string Up() => @"
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Administrator');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Kierownik magazynu');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Kierownik sprzedazy');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Sprzedawca');
INSERT OR IGNORE INTO Uprawnienia (Nazwa) VALUES ('Pracownik magazynu');
";

    public string Down() => "-- brak akcji cofającej (INSERT OR IGNORE jest idempotentny)";
}
