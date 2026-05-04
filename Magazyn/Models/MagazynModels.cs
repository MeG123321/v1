using System.ComponentModel.DataAnnotations;

namespace Magazyn.Models;

public class TowarRodzajDto
{
    public long Id { get; set; }
    public string Nazwa { get; set; } = "";
}

public class JednostkaMiaryDto
{
    public long Id { get; set; }
    public string Nazwa { get; set; } = "";
    public string? Skrot { get; set; }
}

public class StawkaVatDto
{
    public long Id { get; set; }
    public string Nazwa { get; set; } = "";
    public double Wartosc { get; set; }
}

public class RejestracjaTowaruVm
{
    [Required(ErrorMessage = "Nazwa towaru jest wymagana")]
    public string NazwaTowaru { get; set; } = "";

    [Required(ErrorMessage = "Rodzaj towaru jest wymagany")]
    [Range(1, long.MaxValue, ErrorMessage = "Wybierz rodzaj towaru")]
    public long RodzajId { get; set; }

    [Required(ErrorMessage = "Jednostka miary jest wymagana")]
    [Range(1, long.MaxValue, ErrorMessage = "Wybierz jednostkę miary")]
    public long JednostkaMiaryId { get; set; }

    [Required(ErrorMessage = "Ilość jest wymagana")]
    [Range(0.001, double.MaxValue, ErrorMessage = "Ilość musi być większa od 0")]
    public decimal? Ilosc { get; set; }   // <- zmiana (było decimal)

    [Required(ErrorMessage = "Cena netto jest wymagana")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cena netto musi być większa od 0")]
    public decimal? CenaNetto { get; set; } // <- zmiana (było decimal)

    [Required(ErrorMessage = "Stawka VAT jest wymagana")]
    [Range(1, long.MaxValue, ErrorMessage = "Wybierz stawkę VAT")]
    public long StawkaVatId { get; set; }

    public string? Opis { get; set; }
    public string? Dostawca { get; set; }
    public string? DataDostawy { get; set; }

    public List<TowarRodzajDto> Rodzaje { get; set; } = new();
    public List<JednostkaMiaryDto> JednostkiMiary { get; set; } = new();
    public List<StawkaVatDto> StawkiVat { get; set; } = new();
}

public class TowarStanDto
{
    public long TowarId { get; set; }
    public string NazwaTowaru { get; set; } = "";
    public string RodzajTowaru { get; set; } = "";
    public string JednostkaMiary { get; set; } = "";
    public decimal StanMagazynowy { get; set; }
}

public class StanyMagazynoweVm
{
    public string? NazwaTowaru { get; set; }
    public long? RodzajId { get; set; }
    public string? ImiePracownika { get; set; }
    public string? DataStanu { get; set; }
    public List<TowarStanDto> Wyniki { get; set; } = new();
    public List<TowarRodzajDto> Rodzaje { get; set; } = new();
    public bool Searched { get; set; }
}

public class HistoriaWpisDto
{
    public long Id { get; set; }
    public string DataRejestracji { get; set; } = "";
    public string ImieNazwisko { get; set; } = "";
    public decimal Ilosc { get; set; }
}

public class PracownikListDto
{
    public long Id { get; set; }
    public string ImieNazwisko { get; set; } = "";
}

public class HistoriaStanowVm
{
    public long TowarId { get; set; }
    public string NazwaTowaru { get; set; } = "";
    public string? DataOd { get; set; }
    public string? DataDo { get; set; }
    public long? PracownikId { get; set; }
    public List<HistoriaWpisDto> Historia { get; set; } = new();
    public List<PracownikListDto> Pracownicy { get; set; } = new();
    public bool Filtered { get; set; }
}

public class SzczegolyRejestracjiVm
{
    public long Id { get; set; }
    public string NazwaTowaru { get; set; } = "";
    public string RodzajTowaru { get; set; } = "";
    public string JednostkaMiary { get; set; } = "";
    public decimal? Ilosc { get; set; }
    public decimal? CenaNetto { get; set; }
    public string StawkaVat { get; set; } = "";
    public string? Opis { get; set; }
    public string? Dostawca { get; set; }
    public string? DataDostawy { get; set; }
    public string DataRejestracji { get; set; } = "";
    public string ImieNazwiskoPracownika { get; set; } = "";
}

public class ZmianaVatVm
{
    public string Zakres { get; set; } = "TOWAR";
    public long? TowarId { get; set; }
    public long? RodzajId { get; set; }

    [Required(ErrorMessage = "Nowa stawka VAT jest wymagana")]
    [Range(1, long.MaxValue, ErrorMessage = "Wybierz nową stawkę VAT")]
    public long NowaStawkaVatId { get; set; }

    [Required(ErrorMessage = "Data obowiązywania jest wymagana")]
    public string DataObowiazywania { get; set; } = "";

    public string NazwaTowaru { get; set; } = "";
    public string NazwaRodzaju { get; set; } = "";
    public List<StawkaVatDto> StawkiVat { get; set; } = new();
}

public class TowarRodzajVm
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Nazwa rodzaju jest wymagana")]
    public string Nazwa { get; set; } = "";

    public int LiczbaTowarow { get; set; }
}