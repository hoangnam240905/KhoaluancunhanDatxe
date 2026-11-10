namespace Backend.Entities;

/// <summary>
/// The trip's driver says the dispatcher's pre-handover check does not match the real car
/// (plan1.md Phần A). Kept as its own rows so dispute → fix → dispute again stays in history.
/// While one is Open the trip cannot start.
/// </summary>
public class InspectionDispute
{
    public int InspectionDisputeId { get; set; }
    public int BookingId { get; set; }
    /// <summary>The PreHandover inspection being disputed.</summary>
    public int InspectionId { get; set; }
    public int? RaisedByUserId { get; set; }
    public string? RaisedByName { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public int? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public VehicleInspection Inspection { get; set; } = null!;
}
