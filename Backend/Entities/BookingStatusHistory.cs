namespace Backend.Entities;

public class BookingStatusHistory
{
    public int HistoryId { get; set; }
    public int BookingId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public int? ChangedBy { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
