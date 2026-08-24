namespace Backend.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string? Address { get; set; }
    public string? IdNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = [];
}
