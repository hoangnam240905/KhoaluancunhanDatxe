using Backend.Constants;
using Backend.Entities;
using Backend.Validation;

namespace Backend.Services;

/// <summary>
/// Whether a company driver may take a trip (plan1.md Phần D): verified by a dispatcher, licence valid
/// through the return date, and a licence class that covers the vehicle type (Luật 36/2024).
/// Schedule conflicts are checked separately.
/// </summary>
public static class DriverQualification
{
    public const string NotVerified = "Tài xế chưa được xác minh bằng lái.";
    public const string LicenceExpires = "Bằng lái của tài xế hết hạn trước ngày trả xe.";

    public static string? ReasonFor(Driver driver, VehicleType type, DateTime tripEnd)
    {
        if (driver.VerificationStatus != DriverVerificationStatuses.Verified)
            return NotVerified;
        if (string.IsNullOrWhiteSpace(driver.LicenseNumber) || driver.LicenseExpiry < DateOnly.FromDateTime(tripEnd))
            return LicenceExpires;
        return LicenseClasses.CannotDrive(driver.LicenseClass, type.MinLicenseClass, type.SeatCapacity, professional: true);
    }
}
