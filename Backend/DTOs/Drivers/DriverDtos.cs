namespace Backend.DTOs.Drivers;

public record DriverResponse(
    int DriverId,
    string FullName,
    string Email,
    string? Phone,
    string LicenseNumber,
    DateOnly LicenseExpiry,
    string Status,
    decimal AverageRating,
    int TotalTrips);

public record UpdateDriverStatusRequest(string Status);

public record AdminDriverResponse(
    int DriverId,
    string FullName,
    string Email,
    string? Phone,
    string LicenseNumber,
    DateOnly LicenseExpiry,
    string Status,
    decimal AverageRating,
    int TotalTrips,
    bool IsActive);

public record CreateAdminDriverRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? Password);

public record UpdateAdminDriverRequest(
    string? FullName,
    string? Phone,
    string? Status);
