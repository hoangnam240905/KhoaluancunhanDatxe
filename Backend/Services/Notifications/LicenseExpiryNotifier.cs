using Backend.Constants;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Notifications;

/// <summary>
/// BH-1 (plan1.md D.6): tells drivers whose licence expires within 30 days, once per expiry date.
/// Runs shortly after start-up and then twice a day. An expired licence is already blocked from
/// assignment by <see cref="DriverQualification"/>.
/// </summary>
public sealed class LicenseExpiryNotifier(IServiceScopeFactory scopes, ILogger<LicenseExpiryNotifier> logger) : BackgroundService
{
    public const string Title = "Bằng lái sắp hết hạn";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await RunOnceAsync(scope.ServiceProvider.GetRequiredService<CarRentalDbContext>(), DateOnly.FromDateTime(DateTime.Today));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Licence expiry reminders failed");
                }
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <returns>Number of reminders written.</returns>
    public static async Task<int> RunOnceAsync(CarRentalDbContext db, DateOnly today)
    {
        var limit = today.AddDays(30);
        var drivers = await db.Drivers.AsNoTracking()
            .Where(d => d.IsActive && d.LicenseExpiry >= today && d.LicenseExpiry < limit
                && d.VerificationStatus == DriverVerificationStatuses.Verified)
            .Select(d => new { d.DriverId, d.LicenseExpiry })
            .ToListAsync();

        var written = 0;
        foreach (var d in drivers)
        {
            var message = $"Bằng lái của bạn hết hạn ngày {d.LicenseExpiry:dd/MM/yyyy}. Vui lòng cập nhật bằng mới để tiếp tục nhận chuyến.";
            if (await db.Notifications.AnyAsync(n => n.UserId == d.DriverId && n.Title == Title && n.Message == message))
                continue;
            UserNotifications.Add(db, d.DriverId, Title, message);
            written++;
        }
        // Self-drive customers with an approved licence (plan1.md Phần E).
        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.LicenseStatus == CustomerLicenseStatuses.Approved && c.DriverLicenseExpiry != null
                && c.DriverLicenseExpiry >= today && c.DriverLicenseExpiry < limit)
            .Select(c => new { c.CustomerId, c.DriverLicenseExpiry })
            .ToListAsync();
        foreach (var c in customers)
        {
            var message = $"Bằng lái của bạn hết hạn ngày {c.DriverLicenseExpiry:dd/MM/yyyy}. Vui lòng cập nhật bằng mới để tiếp tục thuê xe tự lái.";
            if (await db.Notifications.AnyAsync(n => n.UserId == c.CustomerId && n.Title == Title && n.Message == message))
                continue;
            UserNotifications.Add(db, c.CustomerId, Title, message);
            written++;
        }

        if (written > 0)
            await db.SaveChangesAsync();
        return written;
    }
}
