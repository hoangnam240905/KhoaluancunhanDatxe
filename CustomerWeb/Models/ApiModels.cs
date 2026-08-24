namespace CustomerWeb.Models;

public record LoginRequest(string Email, string Password);

public record RegisterCustomerRequest(
    string Email,
    string Password,
    string FullName,
    string? Phone,
    string? Address,
    string? IdNumber,
    DateOnly? DateOfBirth);

public record AuthResponse(
    string Token,
    int UserId,
    string Email,
    string FullName,
    string Role,
    DateTime ExpiresAt);

public record VehicleTypeResponse(
    int TypeId,
    string TypeName,
    int SeatCapacity,
    decimal PricePerDay,
    decimal PricePerKm,
    string? Description,
    string? ImageUrl);

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
    int VehicleId,
    string LicensePlate,
    string Status,
    DateTime AssignedAt);

public record CreateReviewRequest(byte Rating, string? Comment);

public record ApiError(string Message);
