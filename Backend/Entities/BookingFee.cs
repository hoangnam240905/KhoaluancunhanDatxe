namespace Backend.Entities;

public class BookingFee
{
    public int FeeId { get; set; }
    public int BookingId { get; set; }
    public string FeeType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
