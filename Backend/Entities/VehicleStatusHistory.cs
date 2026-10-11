namespace Backend.Entities;

public class VehicleStatusHistory
{
    public int StatusHistoryId { get; set; }
    public int VehicleId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
