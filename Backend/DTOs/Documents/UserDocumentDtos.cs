namespace Backend.DTOs.Documents;

public record UserDocumentResponse(
    int UserDocumentId,
    int UserId,
    string? UserName,
    string DocumentType,
    string ContentType,
    long SizeBytes,
    string Status,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? RejectReason,
    DateTime CreatedAt,
    int? LicenseId = null);

/// <summary>Approve = true accepts the photo; false rejects it and needs a reason the owner will see.</summary>
public record ReviewDocumentRequest(bool Approve, string? Reason);
