namespace Magazyn.Models.Dtos;

public class UserDetailsDto
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Pesel { get; set; }
    public string? DataUrodzenia { get; set; }
    public string? NrTelefonu { get; set; }
    public int Plec { get; set; }
    public string? Status { get; set; }
    public string? Rola { get; set; }
    public List<string> RoleList { get; set; } = new();
    public string? Miejscowosc { get; set; }
    public string? KodPocztowy { get; set; }
    public string? Ulica { get; set; }
    public string? NrPosesji { get; set; }
    public string? NrLokalu { get; set; }
}
