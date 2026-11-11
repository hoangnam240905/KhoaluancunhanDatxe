using Backend.Constants;
using Backend.Entities;
using Backend.Services;

namespace Backend.Data;

/// <summary>
/// Phase 9, safe to run on every start: every driver / customer that still has a single licence in the old
/// columns and no row in PersonLicenses gets that licence copied as their first (primary) licence, with the
/// same status and reviewer. Photos already sent (no LicenseId) count as that licence's photos. The old
/// columns are kept (mirrored) so older readers keep working; they can be dropped in a later cleanup.
/// </summary>
public static class PersonLicenseBackfill
{
    public static int Run(CarRentalDbContext db)
    {
        var drivers = db.PersonLicenses.Where(l => l.OwnerKind == LicenceOwnerKinds.Driver).Select(l => l.OwnerId).Distinct().ToHashSet();
        var customers = db.PersonLicenses.Where(l => l.OwnerKind == LicenceOwnerKinds.Customer).Select(l => l.OwnerId).Distinct().ToHashSet();
        var added = 0;
        foreach (var d in db.Drivers.Where(d => d.LicenseNumber != "" && d.LicenseClass != null).ToList())
        {
            if (drivers.Contains(d.DriverId))
                continue;
            foreach (var l in LicenceBook.Legacy(d))
            {
                db.PersonLicenses.Add(l);
                added++;
            }
        }
        foreach (var c in db.Customers.Where(c => c.LicenseStatus != CustomerLicenseStatuses.None && c.DriverLicenseNumber != null).ToList())
        {
            if (customers.Contains(c.CustomerId))
                continue;
            foreach (var l in LicenceBook.Legacy(c))
            {
                db.PersonLicenses.Add(l);
                added++;
            }
        }
        if (added > 0)
            db.SaveChanges();
        return added;
    }
}
