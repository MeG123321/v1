namespace Magazyn.Models.Dtos;

public class UserListRowDto
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Pesel { get; set; }
    public string? Status { get; set; }
    public string? Rola { get; set; }
}
