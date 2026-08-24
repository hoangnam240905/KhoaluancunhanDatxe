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
    string? Notes);

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
    TripAssignmentResponse? Assignment);

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

public record AssignTripRequest(int DriverId, int VehicleId);

public record CreateReviewRequest(byte Rating, string? Comment);

public record ReviewResponse(
    int ReviewId,
    int BookingId,
    byte Rating,
    string? Comment,
    DateTime CreatedAt);
