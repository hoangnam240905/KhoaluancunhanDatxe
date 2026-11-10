namespace Backend.DTOs.Dispatch;

/// <summary>A car the booking can be moved to, with the price difference for the remaining days.</summary>
public record VehicleChangeOption(
    int VehicleId,
    string LicensePlate,
    string Brand,
    string Model,
    int TypeId,
    string TypeName,
    int SeatCapacity,
    int CurrentKm,
    bool SameType,
    decimal PriceDifference);

/// <summary>
/// What the dispatcher may choose (plan1.md Phần I): same-type cars first; other types only when no
/// same-type car is free for the rest of the rental. DuringTrip = readings of both cars are required.
/// </summary>
public record VehicleChangeOptionsResponse(
    int BookingId,
    int CurrentVehicleId,
    string CurrentVehicle,
    string CurrentTypeName,
    bool DuringTrip,
    int RemainingDays,
    decimal? SegmentStartKm,
    IReadOnlyList<VehicleChangeOption> SameType,
    IReadOnlyList<VehicleChangeOption> OtherTypes);

public record ChangeVehicleRequest(
    int NewVehicleId,
    string? Reason,
    string? Note,
    decimal? OldOdometerKm,
    decimal? OldFuelLevel,
    decimal? NewOdometerKm,
    decimal? NewFuelLevel,
    bool CustomerAgreed);

/// <summary>One "Phụ lục đổi xe" entry.</summary>
public record VehicleChangeResponse(
    int VehicleChangeId,
    int BookingId,
    string OldVehicle,
    string NewVehicle,
    string OldTypeName,
    string NewTypeName,
    string Reason,
    string? Note,
    bool DuringTrip,
    decimal? OldOdometerKm,
    decimal? OldFuelLevel,
    decimal? NewOdometerKm,
    decimal? NewFuelLevel,
    int RemainingDays,
    decimal PriceDifference,
    bool CustomerAgreed,
    string? ApprovedByName,
    DateTime CreatedAt);
