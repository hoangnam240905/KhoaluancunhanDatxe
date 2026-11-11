namespace Backend.Entities;

/// <summary>
/// A photo of a personal document (driver's licence / ID card, front or back) sent by a driver or a
/// self-drive customer (plan1.md Phần F). The file lives outside the public web folder; only the
/// relative path is stored. Re-sending a side adds a new row, so the newest row per type is current.
/// </summary>
public class UserDocument
{
    public int UserDocumentId { get; set; }
    public int UserId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    /// <summary>Relative to the document storage root, e.g. "12/5f2c….jpg".</summary>
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Status { get; set; } = "Pending";
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Phase 9: the licence a licence photo belongs to; null = the primary licence (older photos) or an ID card.</summary>
    public int? LicenseId { get; set; }

    public User User { get; set; } = null!;
}
