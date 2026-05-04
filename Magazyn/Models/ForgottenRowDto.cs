namespace Magazyn.Models.Dtos;

public class ForgottenRowDto
{
    public long Id { get; set; }
    public string? FullNameAfterForget { get; set; }
    public string? DataZapomnienia { get; set; }
    public string? ZapomnialUserId { get; set; }
    
    // Tego brakowało – dodaj tę linię:
    public string? AdminName { get; set; } 
}