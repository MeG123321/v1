namespace Magazyn.Migrations;

/// <summary>
/// Interfejs dla migracji bazy danych SQLite.
/// Każda migracja implementuje metody Up (zastosowanie) i Down (cofnięcie).
/// </summary>
public interface IMigration
{
    /// <summary>Unikalny identyfikator migracji w formacie YYYYMMDDHHmmss_Nazwa.</summary>
    string Id { get; }

    /// <summary>SQL wykonywany przy zastosowaniu migracji.</summary>
    string Up();

    /// <summary>SQL wykonywany przy cofaniu migracji.</summary>
    string Down();
}
