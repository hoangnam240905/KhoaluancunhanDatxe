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
    string? RentalMode = null);

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
    DateTime CreatedAt);

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
    string? Notes = null);

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

public record CreateReviewRequest(byte Rating, string? Comment);

public record ReviewResponse(
    int ReviewId,
    int BookingId,
    byte Rating,
    string? Comment,
    DateTime CreatedAt);
