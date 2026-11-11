using Backend.DTOs.Documents;

namespace Backend.DTOs.Licences;

/// <summary>
/// One licence of a driver or self-drive customer (Phase 9). Status: Pending / Approved / Rejected;
/// PhotoStatus: Missing / Pending / Accepted / Rejected (this licence's two sides, plus the ID card for drivers).
/// IsPrimary: the first licence, mirrored into the older single-licence fields.
/// </summary>
public record PersonLicenseResponse(
    int LicenseId,
    string Number,
    string Class,
    string ClassLabel,
    DateOnly? IssueDate,
    DateOnly Expiry,
    string Status,
    string PhotoStatus,
    IReadOnlyList<string> MissingDocuments,
    IReadOnlyList<UserDocumentResponse> Documents,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewNote,
    bool IsPrimary,
    bool ExpiringSoon,
    bool Expired);
