namespace Backend.Entities;

/// <summary>
/// Contract appendix "Phụ lục đổi xe" (plan1.md Phần I, contract terms 10–13): the dispatcher replaced the car
/// of a booking, before pickup or during the trip. Readings are filled only for a mid-trip change; the trip's
/// km is then the sum of both legs.
/// </summary>
public class VehicleChange
{
    public int VehicleChangeId { get; set; }
    public int BookingId { get; set; }
    public int OldVehicleId { get; set; }
    public int NewVehicleId { get; set; }
    public int OldTypeId { get; set; }
    public int NewTypeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    /// <summary>True when the booking was already running (in-trip change).</summary>
    public bool DuringTrip { get; set; }
    public decimal? OldOdometerKm { get; set; }
    public decimal? OldFuelLevel { get; set; }
    public decimal? NewOdometerKm { get; set; }
    public decimal? NewFuelLevel { get; set; }
    public int RemainingDays { get; set; }
    /// <summary>(new type day price − old) × remaining days; 0 for the same type. Positive = customer pays more.</summary>
    public decimal PriceDifference { get; set; }
    /// <summary>The customer agreed to the price difference (by phone); ticked by the dispatcher.</summary>
    public bool CustomerAgreed { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public Vehicle OldVehicle { get; set; } = null!;
    public Vehicle NewVehicle { get; set; } = null!;
}
