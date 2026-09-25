namespace Backend.Entities;

public class Review
{
    public int ReviewId { get; set; }
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public int DriverId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
}
