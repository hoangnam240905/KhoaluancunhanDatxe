using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Phase 9 — the licences of a driver or a self-drive customer. A person may hold several; what they may
/// drive is decided by ANY approved licence that covers the class and is valid through the return date.
/// The old single-licence columns on <see cref="Driver"/> / <see cref="Customer"/> are kept as a mirror:
/// number / class / dates of the primary (first) licence and the person-level status (verified = at
/// least one approved licence). A person with no rows yet (data from before Phase 9, not backfilled)
/// is read from those columns.
/// </summary>
public static class LicenceBook
{
    public const string DuplicateOwn = "Bạn đã khai bằng lái số này rồi.";
    public const string NotFound = "Không tìm thấy bằng lái.";

    /// <summary>The licences of this driver: loaded rows, else the single licence of the old columns.</summary>
    public static List<PersonLicense> Of(Driver d) => d.Licences ?? Legacy(d);

    public static List<PersonLicense> Of(Customer c) => c.Licences ?? Legacy(c);

    public static List<PersonLicense> Legacy(Driver d)
    {
        if (string.IsNullOrWhiteSpace(d.LicenseNumber) || string.IsNullOrWhiteSpace(d.LicenseClass))
            return [];
        return
        [
            new PersonLicense
            {
                OwnerKind = LicenceOwnerKinds.Driver, OwnerId = d.DriverId, Number = d.LicenseNumber,
                Class = d.LicenseClass!, IssueDate = d.LicenseIssueDate, Expiry = d.LicenseExpiry,
                Status = d.VerificationStatus switch
                {
                    DriverVerificationStatuses.Verified => PersonLicenseStatuses.Approved,
                    DriverVerificationStatuses.Rejected => PersonLicenseStatuses.Rejected,
                    _ => PersonLicenseStatuses.Pending
                },
                ReviewedByUserId = d.VerifiedByUserId, ReviewedByName = d.VerifiedByName,
                ReviewedAt = d.VerifiedAt, ReviewNote = d.VerificationNote, CreatedAt = DateTime.UtcNow
            }
        ];
    }

    public static List<PersonLicense> Legacy(Customer c)
    {
        if (c.LicenseStatus == CustomerLicenseStatuses.None || string.IsNullOrWhiteSpace(c.DriverLicenseNumber)
            || string.IsNullOrWhiteSpace(c.DriverLicenseClass))
            return [];
        return
        [
            new PersonLicense
            {
                OwnerKind = LicenceOwnerKinds.Customer, OwnerId = c.CustomerId, Number = c.DriverLicenseNumber!,
                Class = c.DriverLicenseClass!, IssueDate = c.DriverLicenseIssueDate,
                Expiry = c.DriverLicenseExpiry ?? DateOnly.MinValue,
                Status = c.LicenseStatus switch
                {
                    CustomerLicenseStatuses.Approved => PersonLicenseStatuses.Approved,
                    CustomerLicenseStatuses.Rejected => PersonLicenseStatuses.Rejected,
                    _ => PersonLicenseStatuses.Pending
                },
                ReviewedByUserId = c.LicenseReviewedByUserId, ReviewedByName = c.LicenseReviewedByName,
                ReviewedAt = c.LicenseReviewedAt, ReviewNote = c.LicenseReviewNote, CreatedAt = DateTime.UtcNow
            }
        ];
    }

    /// <summary>Attach the licence rows to each driver (drivers without rows keep reading the old columns).</summary>
    public static async Task LoadAsync(CarRentalDbContext db, IEnumerable<Driver> drivers)
    {
        var list = drivers.Where(d => d is not null).DistinctBy(d => d.DriverId).ToList();
        if (list.Count == 0)
            return;
        var ids = list.Select(d => d.DriverId).ToList();
        var rows = await db.PersonLicenses.AsNoTracking()
            .Where(l => l.OwnerKind == LicenceOwnerKinds.Driver && ids.Contains(l.OwnerId))
            .OrderBy(l => l.PersonLicenseId).ToListAsync();
        foreach (var d in list)
        {
            var mine = rows.Where(l => l.OwnerId == d.DriverId).ToList();
            d.Licences = mine.Count > 0 ? mine : null;
        }
    }

    public static Task LoadAsync(CarRentalDbContext db, Driver driver) => LoadAsync(db, [driver]);

    public static async Task LoadAsync(CarRentalDbContext db, Customer customer)
    {
        var rows = await db.PersonLicenses.AsNoTracking()
            .Where(l => l.OwnerKind == LicenceOwnerKinds.Customer && l.OwnerId == customer.CustomerId)
            .OrderBy(l => l.PersonLicenseId).ToListAsync();
        customer.Licences = rows.Count > 0 ? rows : null;
    }

    /// <summary>
    /// The owner's licence rows, tracked, in order (first = primary). A person still on the old columns gets
    /// that licence copied into a row first, so editing or adding one never loses the existing licence.
    /// </summary>
    public static async Task<List<PersonLicense>> EnsureRowsAsync(CarRentalDbContext db, Driver driver)
    {
        var rows = await TrackedRowsAsync(db, LicenceOwnerKinds.Driver, driver.DriverId);
        if (rows.Count == 0 && Legacy(driver) is [var legacy])
        {
            db.PersonLicenses.Add(legacy);
            await db.SaveChangesAsync();
            rows.Add(legacy);
        }
        driver.Licences = rows;
        return rows;
    }

    public static async Task<List<PersonLicense>> EnsureRowsAsync(CarRentalDbContext db, Customer customer)
    {
        var rows = await TrackedRowsAsync(db, LicenceOwnerKinds.Customer, customer.CustomerId);
        if (rows.Count == 0 && Legacy(customer) is [var legacy])
        {
            db.PersonLicenses.Add(legacy);
            await db.SaveChangesAsync();
            rows.Add(legacy);
        }
        customer.Licences = rows;
        return rows;
    }

    private static Task<List<PersonLicense>> TrackedRowsAsync(CarRentalDbContext db, string kind, int ownerId)
        => db.PersonLicenses.Where(l => l.OwnerKind == kind && l.OwnerId == ownerId)
            .OrderBy(l => l.PersonLicenseId).ToListAsync();

    /// <summary>Copy the primary licence and the person-level status back into the old driver columns.</summary>
    public static void Mirror(Driver d, IReadOnlyList<PersonLicense> rows)
    {
        if (rows.Count == 0)
            return;
        var primary = rows[0];
        d.LicenseNumber = primary.Number;
        d.LicenseClass = primary.Class;
        d.LicenseIssueDate = primary.IssueDate;
        d.LicenseExpiry = primary.Expiry;
        var approved = rows.Where(l => l.Status == PersonLicenseStatuses.Approved).ToList();
        if (approved.Count > 0)
        {
            var last = approved.OrderByDescending(l => l.ReviewedAt).First();
            d.VerificationStatus = DriverVerificationStatuses.Verified;
            d.VerifiedByUserId = last.ReviewedByUserId;
            d.VerifiedByName = last.ReviewedByName;
            d.VerifiedAt = last.ReviewedAt;
            d.VerificationNote = null;
        }
        else if (rows.All(l => l.Status == PersonLicenseStatuses.Rejected))
        {
            var last = rows.OrderByDescending(l => l.ReviewedAt).First();
            d.VerificationStatus = DriverVerificationStatuses.Rejected;
            d.VerifiedByUserId = last.ReviewedByUserId;
            d.VerifiedByName = last.ReviewedByName;
            d.VerifiedAt = last.ReviewedAt;
            d.VerificationNote = last.ReviewNote;
        }
        else
        {
            d.VerificationStatus = DriverVerificationStatuses.Pending;
            d.VerifiedByUserId = null;
            d.VerifiedByName = null;
            d.VerifiedAt = null;
        }
    }

    /// <summary>Copy the primary licence and the person-level status back into the old customer columns.</summary>
    public static void Mirror(Customer c, IReadOnlyList<PersonLicense> rows)
    {
        if (rows.Count == 0)
            return;
        var primary = rows[0];
        c.DriverLicenseNumber = primary.Number;
        c.DriverLicenseClass = primary.Class;
        c.DriverLicenseIssueDate = primary.IssueDate;
        c.DriverLicenseExpiry = primary.Expiry;
        c.LicenseStatus = rows.Any(l => l.Status == PersonLicenseStatuses.Approved) ? CustomerLicenseStatuses.Approved
            : rows.Any(l => l.Status == PersonLicenseStatuses.Pending) ? CustomerLicenseStatuses.Pending
            : CustomerLicenseStatuses.Rejected;
        var reviewed = rows.Where(l => l.ReviewedAt is not null).OrderByDescending(l => l.ReviewedAt).FirstOrDefault();
        c.LicenseReviewedByUserId = reviewed?.ReviewedByUserId;
        c.LicenseReviewedByName = reviewed?.ReviewedByName;
        c.LicenseReviewedAt = reviewed?.ReviewedAt;
        c.LicenseReviewNote = c.LicenseStatus == CustomerLicenseStatuses.Rejected ? reviewed?.ReviewNote : null;
    }

    /// <summary>
    /// One licence number belongs to one account (Phase 9): taken when any other driver or customer holds it
    /// (licence rows or the old columns). Returns <see cref="DuplicateOwn"/> when the same person already
    /// declared it on another licence, the shared "taken" message when another account did, else null.
    /// </summary>
    public static async Task<string?> NumberProblemAsync(CarRentalDbContext db, string number, string kind, int ownerId, int? exceptLicenseId)
    {
        if (await db.PersonLicenses.AnyAsync(l => l.Number == number && l.OwnerKind == kind && l.OwnerId == ownerId
                && (exceptLicenseId == null || l.PersonLicenseId != exceptLicenseId)))
            return DuplicateOwn;
        var isDriver = kind == LicenceOwnerKinds.Driver;
        var taken = await db.PersonLicenses.AnyAsync(l => l.Number == number && !(l.OwnerKind == kind && l.OwnerId == ownerId))
            || await db.Drivers.AnyAsync(d => d.LicenseNumber == number && !(isDriver && d.DriverId == ownerId))
            || await db.Customers.AnyAsync(c => c.DriverLicenseNumber == number && !(!isDriver && c.CustomerId == ownerId));
        return taken ? Validation.LicenseNumberRules.Taken : null;
    }

    /// <summary>
    /// Admin edited the old single-licence fields: carry them into the primary licence row (when the person
    /// already has rows), then mirror back. <paramref name="statusSet"/>: the admin also set the verification status.
    /// </summary>
    public static async Task SyncPrimaryFromColumnsAsync(CarRentalDbContext db, Driver d, bool statusSet)
    {
        var rows = await TrackedRowsAsync(db, LicenceOwnerKinds.Driver, d.DriverId);
        if (rows.Count == 0)
            return;
        var p = rows[0];
        if (!string.IsNullOrWhiteSpace(d.LicenseNumber))
            p.Number = d.LicenseNumber;
        if (!string.IsNullOrWhiteSpace(d.LicenseClass))
            p.Class = d.LicenseClass!;
        p.IssueDate = d.LicenseIssueDate;
        p.Expiry = d.LicenseExpiry;
        if (statusSet)
            p.Status = Legacy(d) is [var l] ? l.Status : p.Status;
        Mirror(d, rows);
    }

    public static async Task SyncPrimaryFromColumnsAsync(CarRentalDbContext db, Customer c)
    {
        var rows = await TrackedRowsAsync(db, LicenceOwnerKinds.Customer, c.CustomerId);
        if (rows.Count == 0)
            return;
        var p = rows[0];
        if (!string.IsNullOrWhiteSpace(c.DriverLicenseNumber))
            p.Number = c.DriverLicenseNumber!;
        if (!string.IsNullOrWhiteSpace(c.DriverLicenseClass))
            p.Class = c.DriverLicenseClass!;
        p.IssueDate = c.DriverLicenseIssueDate;
        if (c.DriverLicenseExpiry is DateOnly e)
            p.Expiry = e;
        Mirror(c, rows);
    }

    public static bool IsPrimary(IReadOnlyList<PersonLicense> rows, PersonLicense licence)
        => rows.Count > 0 && (ReferenceEquals(rows[0], licence)
            || (licence.PersonLicenseId != 0 && rows[0].PersonLicenseId == licence.PersonLicenseId));
}
