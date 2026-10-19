namespace Backend.Entities;

public class CustomerVehicleInteraction
{
    public int InteractionId { get; set; }
    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public string InteractionType { get; set; } = string.Empty;
    /// <summary>Set for server-recorded BOOK/RENT/CANCEL; null for client events (VIEW, CLICK_BOOK…).</summary>
    public int? BookingId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
