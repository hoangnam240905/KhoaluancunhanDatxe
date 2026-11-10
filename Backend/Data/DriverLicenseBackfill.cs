using Backend.Constants;
using Backend.Validation;

namespace Backend.Data;

/// <summary>
/// One-time fill when licence verification arrived (plan1.md Phần D), safe to run on every start:
/// it only touches rows that still have no licence class. Never deletes data.
/// - Vehicle types: minimum class from the seat count (4/7/9 seats → B, 16 → D1, 29 → D2).
/// - Sample drivers with an old "B2-…" licence: class B2 (old licence still valid), verified.
/// - Demo driver #9 (made by admin with a placeholder number): class D1 so the 16-seat van has a driver, verified.
/// - Other drivers with a placeholder "DRV-…" number (e.g. #10): number cleared, stay "Chờ xác minh" so the
///   real licence is entered and checked — the demo of the verification flow.
/// </summary>
public static class DriverLicenseBackfill
{
    public static int Run(CarRentalDbContext db)
    {
        var changed = 0;
        foreach (var type in db.VehicleTypes.Where(t => t.MinLicenseClass == null).ToList())
        {
            type.MinLicenseClass = LicenseClasses.MinimumForSeats(type.SeatCapacity);
            changed++;
        }

        foreach (var driver in db.Drivers.Where(d => d.LicenseClass == null).ToList())
        {
            if (driver.LicenseNumber.StartsWith("B2-", StringComparison.OrdinalIgnoreCase))
            {
                driver.LicenseClass = "B2";
                driver.VerificationStatus = DriverVerificationStatuses.Verified;
                changed++;
            }
            else if (driver.DriverId == 9 && driver.LicenseNumber == "DRV-000009")
            {
                driver.LicenseClass = "D1";
                driver.LicenseNumber = "790123000009";
                driver.LicenseIssueDate = new DateOnly(2025, 3, 1);
                driver.LicenseExpiry = new DateOnly(2035, 3, 1);
                driver.VerificationStatus = DriverVerificationStatuses.Verified;
                changed++;
            }
            else if (driver.LicenseNumber.StartsWith("DRV-", StringComparison.OrdinalIgnoreCase))
            {
                driver.LicenseNumber = string.Empty;
                driver.LicenseExpiry = DateOnly.MinValue;
                driver.VerificationStatus = DriverVerificationStatuses.Pending;
                changed++;
            }
        }

        // plan1.md Phần E: the two sample customers get an approved class B licence; others stay "chưa khai".
        var sampleEmails = new[] { "customer1@gmail.com", "customer2@gmail.com" };
        var samples = db.Customers
            .Where(c => c.LicenseStatus == CustomerLicenseStatuses.None && c.DriverLicenseNumber == null
                && sampleEmails.Contains(c.User.Email))
            .ToList();
        foreach (var c in samples)
        {
            c.DriverLicenseNumber = $"7903{c.CustomerId:D8}";
            c.DriverLicenseClass = "B";
            c.DriverLicenseExpiry = new DateOnly(2034, 12, 31);
            c.LicenseStatus = CustomerLicenseStatuses.Approved;
            changed++;
        }
        // A licence typed in earlier (admin page) but never reviewed waits for approval.
        foreach (var c in db.Customers.Where(c => c.LicenseStatus == CustomerLicenseStatuses.None && c.DriverLicenseNumber != null).ToList())
        {
            c.LicenseStatus = CustomerLicenseStatuses.Pending;
            changed++;
        }

        if (changed > 0)
            db.SaveChanges();
        return changed;
    }
}
