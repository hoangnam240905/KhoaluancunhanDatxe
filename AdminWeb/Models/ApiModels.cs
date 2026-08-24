namespace AdminWeb.Models;

public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, int UserId, string Email, string FullName, string Role, DateTime ExpiresAt);
public record ApiError(string Message);

public record VehicleTypeResponse(int TypeId, string TypeName, int SeatCapacity, decimal PricePerDay, decimal PricePerKm, string? Description, string? ImageUrl);
public record VehicleResponse(int VehicleId, int TypeId, string TypeName, string LicensePlate, string Brand, string Model, int Year, string? Color, string Status, int CurrentKm);
public record CreateVehicleRequest(int TypeId, string LicensePlate, string Brand, string Model, int Year, string? Color, int CurrentKm);
public record UpdateVehicleRequest(int TypeId, string LicensePlate, string Brand, string Model, int Year, string? Color, string Status, int CurrentKm);

public record BookingResponse(
    int BookingId, int CustomerId, string CustomerName, int VehicleTypeId, string VehicleTypeName,
    string PickupAddress, string DropoffAddress, DateTime StartDate, DateTime EndDate,
    decimal? EstimatedDistance, decimal TotalAmount, string Status, string? Notes, DateTime CreatedAt,
    TripAssignmentResponse? Assignment);

public record TripAssignmentResponse(int AssignmentId, int DriverId, string DriverName, int VehicleId, string LicensePlate, string Status, DateTime AssignedAt);
public record UpdateBookingStatusRequest(string Status, string? Note);
