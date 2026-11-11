using Backend.Constants;
using Backend.Entities;
using Backend.Validation;

namespace Backend.Services;

/// <summary>
/// Whether a driver may be put on a trip with a given vehicle type (plan1.md Phần D, Phase 9): the driver has
/// at least one verified licence, and ANY verified licence covers the type (Luật 36/2024; old licences still
/// valid; professional driving rule) and stays valid until the return date.
/// </summary>
public static class DriverQualification
{
    public const string NotVerified = "Tài xế chưa được xác minh bằng lái.";
    public const string LicenceExpires = "Bằng lái của tài xế hết hạn trước ngày trả xe.";

    /// <summary>Reason the driver cannot take this trip, or null when qualified. Load licences with <see cref="LicenceBook.LoadAsync(Data.CarRentalDbContext, Driver)"/> first.</summary>
    public static string? ReasonFor(Driver driver, VehicleType type, DateTime tripEnd)
        => ReasonFor(LicenceBook.Of(driver), type, tripEnd);

    public static string? ReasonFor(IReadOnlyList<PersonLicense> licences, VehicleType type, DateTime tripEnd)
    {
        var approved = licences.Where(l => l.Status == PersonLicenseStatuses.Approved && !string.IsNullOrWhiteSpace(l.Number)).ToList();
        if (approved.Count == 0)
            return NotVerified;
        var end = DateOnly.FromDateTime(tripEnd);
        var covering = approved
            .Where(l => LicenseClasses.CannotDrive(l.Class, type.MinLicenseClass, type.SeatCapacity, professional: true) is null)
            .ToList();
        if (covering.Any(l => l.Expiry >= end))
            return null;
        if (covering.Count > 0)
            return LicenceExpires;
        // No verified licence covers the type: explain with the widest one.
        var widest = approved.OrderByDescending(l => LicenseClasses.Find(l.Class)?.MaxSeats ?? 0).First();
        return LicenseClasses.CannotDrive(widest.Class, type.MinLicenseClass, type.SeatCapacity, professional: true);
    }
}
