namespace Backend.DTOs.Bookings;

public record CreateBookingRequest(
    int VehicleTypeId,
    string PickupAddress,
    string DropoffAddress,
    decimal? PickupLat,
    decimal? PickupLng,
    decimal? DropoffLat,
    decimal? DropoffLng,
    DateTime StartDate,
    DateTime EndDate,
    decimal? EstimatedDistance,
    string? Notes,
    string? RentalMode = null,
    int? VehicleId = null);

public record BookingQuoteResponse(
    int VehicleTypeId,
    string VehicleTypeName,
    string RentalMode,
    DateTime StartDate,
    DateTime EndDate,
    decimal QuotedPricePerDay,
    decimal QuotedPricePerKm,
    int QuotedDays,
    decimal EstimatedDistance,
    decimal RentalAmount,
    decimal DistanceAmount,
    decimal TotalAmount,
    decimal DriverAmount,
    decimal IncludedKm,
    decimal ExtraKm,
    decimal ExtraKmPrice,
    decimal DepositAmount);

public record AssignedVehicleResponse(
    int VehicleId,
    string LicensePlate,
    string Brand,
    string Model,
    string Status);

public record BookingResponse(
    int BookingId,
    int CustomerId,
    string CustomerName,
    int VehicleTypeId,
    string VehicleTypeName,
    string PickupAddress,
    string DropoffAddress,
    DateTime StartDate,
    DateTime EndDate,
    decimal? EstimatedDistance,
    decimal TotalAmount,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    TripAssignmentResponse? Assignment,
    string RentalMode,
    AssignedVehicleResponse? AssignedVehicle,
    decimal? QuotedPricePerDay,
    decimal? QuotedPricePerKm,
    int? QuotedDays,
    decimal? QuotedDriverFeePerDay,
    decimal? QuotedSelfDriveIncludedKmPerDay,
    decimal? QuotedSelfDriveExtraKmPrice,
    decimal? QuotedDepositAmount,
    decimal? FinalAmount,
    IReadOnlyList<BookingFeeResponse> Fees,
    decimal? FinalBaseAmount,
    decimal? TotalFees,
    IReadOnlyList<VehicleInspectionResponse> Inspections);

public record VehicleInspectionResponse(
    int InspectionId,
    int BookingId,
    int VehicleId,
    string InspectionType,
    DateTime ActualAt,
    decimal? OdometerKm,
    decimal? FuelLevel,
    string? Condition,
    string? Notes,
    DateTime CreatedAt,
    string? ExteriorCondition = null,
    string? TechnicalCondition = null);

public record BookingFeeResponse(
    int FeeId,
    string FeeType,
    string? Description,
    decimal Amount,
    DateTime CreatedAt);

public record VehicleConditionRequest(
    decimal? OdometerKm = null,
    decimal? FuelLevel = null,
    string? Condition = null,
    string? Notes = null,
    string? ExteriorCondition = null,
    string? TechnicalCondition = null);

public record TripAssignmentResponse(
    int AssignmentId,
    int DriverId,
    string DriverName,
    string? DriverPhone,
    int VehicleId,
    string LicensePlate,
    string Status,
    DateTime AssignedAt);

public record UpdateBookingStatusRequest(string Status, string? Note);

public record AssignTripRequest(int? DriverId, int VehicleId);

public record VehicleAlternativeResponse(
    int VehicleId,
    string LicensePlate,
    string Brand,
    string Model,
    string Status);

public record DriverAlternativeResponse(
    int DriverId,
    string FullName,
    string? Phone,
    string Status);

public record AssignConflictResponse(
    string Message,
    string ConflictType,
    IReadOnlyList<VehicleAlternativeResponse> VehicleAlternatives,
    IReadOnlyList<DriverAlternativeResponse> DriverAlternatives);

public sealed record AssignTripResult(
    BookingResponse? Booking,
    string? Error,
    AssignConflictResponse? Conflict)
{
    public static AssignTripResult Ok(BookingResponse booking) => new(booking, null, null);

    public static AssignTripResult Fail(string error) => new(null, error, null);

    public static AssignTripResult FromConflict(AssignConflictResponse conflict)
        => new(null, conflict.Message, conflict);
}

public record CreateReviewRequest(byte Rating, string? Comment);

public record ReviewResponse(
    int ReviewId,
    int BookingId,
    byte Rating,
    string? Comment,
    DateTime CreatedAt);
