using Backend.Data;
using Microsoft.Data.Sqlite;

namespace Backend.Services.Notifications;

/// <summary>Background writer: queue → Notification rows. Failures are logged, never thrown to callers.</summary>
public sealed class NotificationWriter(
    NotificationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<NotificationWriter> logger) : BackgroundService
{
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in queue.Reader.ReadAllAsync(stoppingToken))
            {
                for (var attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
                        await NotificationFanout.WriteAsync(db, evt, stoppingToken);
                        break;
                    }
                    catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6 && attempt < MaxAttempts)
                    {
                        // SQLITE_BUSY / SQLITE_LOCKED: the business transaction may still hold the write lock.
                        await Task.Delay(200 * attempt, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Could not write notification for {EventType} booking {BookingId}",
                            evt.EventType, evt.BookingId);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
