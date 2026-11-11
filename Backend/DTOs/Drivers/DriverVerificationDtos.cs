using Backend.DTOs.Documents;

namespace Backend.DTOs.Drivers;

/// <summary>A driver signs up in the app; the account starts "Chờ xác minh" (plan1.md Phần D).</summary>
public record RegisterDriverRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? Password,
    string? ConfirmPassword = null);

/// <summary>What the driver types from their licence; class codes come from GET /api/drivers/license-classes.</summary>
public record UpdateDriverLicenseRequest(
    string? LicenseNumber,
    string? LicenseClass,
    DateOnly? LicenseIssueDate,
    DateOnly? LicenseExpiry,
    string? IdNumber);

public record LicenseClassOption(string Code, string Label, int MaxSeats, bool AllowedForDrivers);

/// <summary>
/// One driver's verification file: declared licence, the photos and both review steps.
/// PhotoStatus: Missing / Pending / Accepted / Rejected (step 1); VerificationStatus: Pending / Verified / Rejected (step 2).
/// </summary>
public record DriverVerificationResponse(
    int DriverId,
    string FullName,
    string Email,
    string? Phone,
    string? LicenseNumber,
    string? LicenseClass,
    string? LicenseClassLabel,
    DateOnly? LicenseIssueDate,
    DateOnly? LicenseExpiry,
    string? IdNumber,
    string VerificationStatus,
    string PhotoStatus,
    IReadOnlyList<string> MissingDocuments,
    IReadOnlyList<UserDocumentResponse> Documents,
    string? VerifiedByName,
    DateTime? VerifiedAt,
    string? VerificationNote,
    bool LicenseExpiringSoon,
    bool LicenseExpired,
    bool LicenseInfoComplete,
    bool IsActive,
    IReadOnlyList<Backend.DTOs.Licences.PersonLicenseResponse>? Licences = null);

public record ReviewDriverPhotosRequest(bool Accept, string? Reason);

public record VerifyDriverRequest(bool Verified, string? Reason);
