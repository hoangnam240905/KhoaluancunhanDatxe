namespace Backend.Entities;

/// <summary>
/// One driving licence of a driver or a self-drive customer (Phase 9: a person may hold several).
/// A person may drive a vehicle type when ANY approved licence covers the class and is valid through
/// the return date. The old single-licence columns on Driver / Customer stay as a mirror of the
/// first (primary) licence plus the person-level status, so older readers keep working.
/// </summary>
public class PersonLicense
{
    public int PersonLicenseId { get; set; }
    /// <summary>"Driver" or "Customer" (see <see cref="Constants.LicenceOwnerKinds"/>).</summary>
    public string OwnerKind { get; set; } = string.Empty;
    /// <summary>DriverId or CustomerId (both equal the UserId).</summary>
    public int OwnerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public DateOnly? IssueDate { get; set; }
    public DateOnly Expiry { get; set; }
    /// <summary>Pending / Approved / Rejected.</summary>
    public string Status { get; set; } = "Pending";
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
