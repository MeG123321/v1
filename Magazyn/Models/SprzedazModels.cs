using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class SprzedazPozycjaVm
{
    public long TowarId { get; set; }
    public string NazwaTowaru { get; set; } = "";
    public string JednostkaMiary { get; set; } = "";
    public decimal DostepnaIlosc { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Ilość nie może być ujemna")]
    public decimal? Ilosc { get; set; }
}

public class RejestracjaSprzedazyVm
{
    [Required(ErrorMessage = "Nazwa klienta jest wymagana")]
    public string NazwaKlienta { get; set; } = "";

    [Required(ErrorMessage = "Adres klienta jest wymagany")]
    public string AdresKlienta { get; set; } = "";

    [Required(ErrorMessage = "Data sprzedaży jest wymagana")]
    public string DataSprzedazy { get; set; } = "";

    public List<SprzedazPozycjaVm> Pozycje { get; set; } = new();
}

public class SprzedazHistoriaRowDto
{
    public long Id { get; set; }
    public string DataSprzedazy { get; set; } = "";
    public string Nabywca { get; set; } = "";
    public string Sprzedawca { get; set; } = "";
    public int LiczbaPozycji { get; set; }
}

public class HistoriaSprzedazyVm
{
    public string? DataOd { get; set; }
    public string? DataDo { get; set; }
    public string? Nabywca { get; set; }
    public string? Sprzedawca { get; set; }
    public string? Towar { get; set; }
    public bool Filtered { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SprzedazHistoriaRowDto> Wyniki { get; set; } = new();
}

public class SprzedazPozycjaSzczegolDto
{
    public string NazwaTowaru { get; set; } = "";
    public string JednostkaMiary { get; set; } = "";
    public decimal Ilosc { get; set; }
}

public class SzczegolySprzedazyVm
{
    public long Id { get; set; }
    public string NazwaKlienta { get; set; } = "";
    public string AdresKlienta { get; set; } = "";
    public string DataSprzedazy { get; set; } = "";
    public string DataRejestracji { get; set; } = "";
    public string Sprzedawca { get; set; } = "";
    public List<SprzedazPozycjaSzczegolDto> Pozycje { get; set; } = new();
}
