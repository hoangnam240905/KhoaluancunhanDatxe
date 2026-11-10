namespace Backend.Entities;

public class MaintenanceRecord
{
    public int MaintenanceId { get; set; }
    public int VehicleId { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? OdometerAtMaintenance { get; set; }
    public int? ScheduledOdometer { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public int? NextMaintenanceOdometer { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Who opened and who completed the record (from the JWT user at the time).</summary>
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string? CreatedByRole { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? CompletedByName { get; set; }
    public string? CompletedByRole { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
