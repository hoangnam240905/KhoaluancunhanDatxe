using Backend.DTOs.Documents;

namespace Backend.DTOs.Customers;

/// <summary>What the customer types from their licence (class codes from GET /api/drivers/license-classes).</summary>
public record UpdateCustomerLicenseRequest(
    string? LicenseNumber,
    string? LicenseClass,
    DateOnly? LicenseIssueDate,
    DateOnly? LicenseExpiry);

/// <summary>Whether this licence may drive a vehicle type self-drive; Reason says why not.</summary>
public record SelfDriveTypeEligibility(
    int TypeId,
    string TypeName,
    int SeatCapacity,
    string MinLicenseClass,
    bool CanDrive,
    string? Reason);

/// <summary>
/// A customer's self-drive licence file (plan1.md Phần E). LicenseStatus: None / Pending / Approved / Rejected;
/// PhotoStatus: Missing / Pending / Accepted / Rejected. VehicleTypes lists which types they may drive.
/// </summary>
public record CustomerLicenseResponse(
    int CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? LicenseNumber,
    string? LicenseClass,
    string? LicenseClassLabel,
    DateOnly? LicenseIssueDate,
    DateOnly? LicenseExpiry,
    string LicenseStatus,
    string PhotoStatus,
    IReadOnlyList<string> MissingDocuments,
    IReadOnlyList<UserDocumentResponse> Documents,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewNote,
    bool LicenseExpiringSoon,
    bool LicenseExpired,
    IReadOnlyList<SelfDriveTypeEligibility> VehicleTypes);

public record ReviewCustomerLicenseRequest(bool Approve, string? Reason);
