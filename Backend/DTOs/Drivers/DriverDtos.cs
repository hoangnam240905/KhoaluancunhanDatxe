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

public record CustomerDriverOptionResponse(
    int DriverId,
    string FullName,
    string? Phone,
    decimal AverageRating,
    int TotalTrips,
    string LicenseNumber,
    DateOnly LicenseExpiry,
    string EligibilityStatus,
    string AvailabilityStatus,
    string Status = "",
    double? ExperienceYears = null,
    bool CanSelect = true);

public record DriverBusyPeriodResponse(
    DateTime StartDate,
    DateTime EndDate,
    string Label,
    int? BookingId = null,
    string? Status = null);

public record DriverScheduleResponse(
    int DriverId,
    string FullName,
    IReadOnlyList<DriverBusyPeriodResponse> Busy,
    decimal AverageRating = 0,
    int TotalTrips = 0);

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
    bool IsActive,
    string? LicenseClass = null,
    DateOnly? LicenseIssueDate = null,
    string? IdNumber = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    string? AvatarUrl = null,
    double? ExperienceYears = null,
    double? OnTimeRate = null,
    double? TotalDistanceKm = null,
    string VerificationStatus = "Pending");

public record CreateAdminDriverRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? Password,
    string? LicenseClass = null,
    DateOnly? LicenseIssueDate = null,
    string? IdNumber = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    string? AvatarUrl = null,
    double? ExperienceYears = null,
    double? OnTimeRate = null,
    double? TotalDistanceKm = null,
    string? VerificationStatus = null);

public record UpdateAdminDriverRequest(
    string? FullName,
    string? Phone,
    string? Status,
    string? LicenseClass = null,
    DateOnly? LicenseIssueDate = null,
    string? IdNumber = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    string? AvatarUrl = null,
    double? ExperienceYears = null,
    double? OnTimeRate = null,
    double? TotalDistanceKm = null,
    string? VerificationStatus = null);
