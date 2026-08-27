namespace PortalWeb.Models;

public record LoginRequest(string Email, string Password);
public record RegisterCustomerRequest(string Email, string Password, string FullName, string? Phone, string? Address, string? IdNumber, DateOnly? DateOfBirth);
public record AuthResponse(string Token, int UserId, string Email, string FullName, string Role, DateTime ExpiresAt);
public record ApiError(string Message);

public record VehicleTypeResponse(int TypeId, string TypeName, int SeatCapacity, decimal PricePerDay, decimal PricePerKm, string? Description, string? ImageUrl);
public record VehicleResponse(int VehicleId, int TypeId, string TypeName, string LicensePlate, string Brand, string Model, int Year, string? Color, string Status, int CurrentKm);
public record CreateVehicleRequest(int TypeId, string LicensePlate, string Brand, string Model, int Year, string? Color, int CurrentKm);
public record UpdateVehicleRequest(int TypeId, string LicensePlate, string Brand, string Model, int Year, string? Color, string Status, int CurrentKm);

public record CreateBookingRequest(int VehicleTypeId, string PickupAddress, string DropoffAddress, decimal? PickupLat, decimal? PickupLng, decimal? DropoffLat, decimal? DropoffLng, DateTime StartDate, DateTime EndDate, decimal? EstimatedDistance, string? Notes, string? RentalMode = null);
public record AssignedVehicleResponse(int VehicleId, string LicensePlate, string Brand, string Model, string Status);
public record BookingResponse(int BookingId, int CustomerId, string CustomerName, int VehicleTypeId, string VehicleTypeName, string PickupAddress, string DropoffAddress, DateTime StartDate, DateTime EndDate, decimal? EstimatedDistance, decimal TotalAmount, string Status, string? Notes, DateTime CreatedAt, TripAssignmentResponse? Assignment, string RentalMode = "WithDriver", AssignedVehicleResponse? AssignedVehicle = null);
public record TripAssignmentResponse(int AssignmentId, int DriverId, string DriverName, string? DriverPhone, int VehicleId, string LicensePlate, string Status, DateTime AssignedAt);
public record UpdateBookingStatusRequest(string Status, string? Note);
public record AssignTripRequest(int? DriverId, int VehicleId);
public record CreateReviewRequest(byte Rating, string? Comment);
public record DriverResponse(int DriverId, string FullName, string Email, string? Phone, string LicenseNumber, DateOnly LicenseExpiry, string Status, decimal AverageRating, int TotalTrips);
