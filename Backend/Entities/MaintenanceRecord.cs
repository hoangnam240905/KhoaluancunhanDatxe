namespace Backend.Entities;

public class MaintenanceRecord
{
    public int MaintenanceId { get; set; }
    public int VehicleId { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? OdometerAtMaintenance { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
