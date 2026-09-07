namespace Backend.Entities;

public class Contract
{
    public int ContractId { get; set; }
    public int BookingId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Issued";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string VehicleTypeName { get; set; } = string.Empty;
    public string RentalMode { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string DropoffAddress { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SignedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
