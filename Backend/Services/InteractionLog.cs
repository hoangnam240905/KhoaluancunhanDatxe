using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Server-side rental history (BOOK / RENT / CANCEL) used by recommendations.
/// Logging must never break the booking flow: failures are swallowed and the row is detached.
/// Call after the main SaveChanges so the booking itself is already persisted.
/// </summary>
public static class InteractionLog
{
    public const string Book = "BOOK";
    public const string Rent = "RENT";
    public const string Cancel = "CANCEL";
    /// <summary>Customer clicked "Đặt xe này" on the recommendation page (intent, booking may never exist).</summary>
    public const string ClickBook = "CLICK_BOOK";

    /// <summary>Types only the server may write (clients posting them would forge rental history).</summary>
    public static readonly HashSet<string> ServerOnlyTypes = new(StringComparer.OrdinalIgnoreCase) { Book, Rent, Cancel };

    /// <summary>Records one event per (booking, type). Returns true when a row was written.</summary>
    public static async Task<bool> TryRecordAsync(
        CarRentalDbContext db, int customerId, int vehicleId, string type, int bookingId)
    {
        CustomerVehicleInteraction? row = null;
        try
        {
            if (await db.CustomerVehicleInteractions.AnyAsync(i => i.BookingId == bookingId && i.InteractionType == type))
                return false;

            row = new CustomerVehicleInteraction
            {
                CustomerId = customerId,
                VehicleId = vehicleId,
                InteractionType = type,
                BookingId = bookingId,
                CreatedAt = DateTime.UtcNow
            };
            db.CustomerVehicleInteractions.Add(row);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            if (row is not null)
                db.Entry(row).State = EntityState.Detached;
            return false;
        }
    }
}
