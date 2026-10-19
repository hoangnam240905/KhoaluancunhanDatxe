using Backend.Data;
using Backend.DTOs.Recommendations;
using Backend.Options;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Recommendation;

/// <summary>
/// Turns server-recorded BOOK / RENT / CANCEL events into the history list sent to V7 Python.
/// One entry per booking: RENT (car actually received) outweighs BOOK; a cancelled booking that was
/// never received is dropped. Older bookings decay with a half-life. Client events (VIEW, CLICK_BOOK)
/// and legacy rows without BookingId are ignored.
/// </summary>
public static class RentalHistoryBuilder
{
    private sealed record Event(int BookingId, int VehicleId, string Type, DateTime CreatedAt, int? Seats);

    public static async Task<IReadOnlyList<PythonHistoryItem>> BuildAsync(
        CarRentalDbContext db,
        int customerId,
        IReadOnlyDictionary<int, string> carIdOfVehicle,
        RecommendationServiceOptions options,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var events = await db.CustomerVehicleInteractions
            .AsNoTracking()
            .Where(i => i.CustomerId == customerId
                && i.BookingId != null
                && (i.InteractionType == InteractionLog.Book
                    || i.InteractionType == InteractionLog.Rent
                    || i.InteractionType == InteractionLog.Cancel))
            .Select(i => new Event(
                i.BookingId!.Value,
                i.VehicleId,
                i.InteractionType,
                i.CreatedAt,
                i.Vehicle.VehicleType.SeatCapacity))
            .ToListAsync(cancellationToken);

        var halfLife = Math.Max(1, options.HistoryHalfLifeDays);
        return events
            .GroupBy(e => e.BookingId)
            .Select(g => Pick(g.ToList(), options))
            .Where(p => p.Event is not null)
            .OrderByDescending(p => p.Event!.CreatedAt)
            .Take(Math.Max(0, options.MaxHistoryBookings))
            .Select(p =>
            {
                var e = p.Event!;
                var ageDays = Math.Max(0, (utcNow - e.CreatedAt).TotalDays);
                var weight = p.Weight * Math.Pow(0.5, ageDays / halfLife);
                carIdOfVehicle.TryGetValue(e.VehicleId, out var carId);
                return new PythonHistoryItem(carId, e.Seats is > 0 ? e.Seats : null, Math.Round(weight, 6));
            })
            .Where(i => i.Weight > 0)
            .ToList();
    }

    private static (Event? Event, double Weight) Pick(IReadOnlyList<Event> perBooking, RecommendationServiceOptions options)
    {
        var rent = perBooking.FirstOrDefault(e => e.Type == InteractionLog.Rent);
        if (rent is not null)
            return (rent, options.RentEventWeight);
        if (perBooking.Any(e => e.Type == InteractionLog.Cancel))
            return (null, 0);
        var book = perBooking.FirstOrDefault(e => e.Type == InteractionLog.Book);
        return book is null ? (null, 0) : (book, options.BookEventWeight);
    }

    /// <summary>VehicleId → car_id (first car_id wins when several map to one vehicle).</summary>
    public static IReadOnlyDictionary<int, string> Reverse(IReadOnlyDictionary<string, int> carIdToVehicle)
        => carIdToVehicle
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).First().Key);
}
