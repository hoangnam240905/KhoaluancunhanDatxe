using CustomerWeb.Models;
using CustomerWeb.Services;

namespace CustomerWeb.Display;

public static class VehicleSearchSupport
{
    /// <summary>Matches Backend.ScheduleBuffers.TechnicalHours — display only; booking still validated by API.</summary>
    public static readonly TimeSpan TechnicalBuffer = TimeSpan.FromHours(2);

    public static DateTime DefaultStart() => DateTime.Now.AddDays(1).Date.AddHours(8);

    public static DateTime DefaultEnd(DateTime start) => start.AddDays(1);

    public static int? SeatMin(IEnumerable<int>? seats)
    {
        var values = (seats ?? []).Where(s => s > 0).ToList();
        return values.Count == 0 ? null : values.Min();
    }

    public static IReadOnlyList<int> PositiveIds(IEnumerable<int>? ids)
        => (ids ?? []).Where(id => id > 0).Distinct().ToList();

    /// <summary>
    /// Catalog-style load: Backend omits vehicles unavailable for the rental window.
    /// </summary>
    public static async Task<(IReadOnlyList<CatalogVehicle> Results, string? Error)> LoadAsync(
        CarRentalApiClient api,
        IReadOnlyList<VehicleTypeResponse> types,
        IReadOnlyList<int>? typeIds,
        IReadOnlyList<int>? seats,
        decimal? priceMax,
        DateTime? startDate,
        DateTime? endDate)
    {
        var (vehicles, error) = await api.SearchVehiclesAsync(
            "Available",
            PositiveIds(typeIds),
            SeatMin(seats),
            priceMax,
            startDate,
            endDate);
        if (error is not null)
            return ([], error);
        return (CreateBookingUi.FromVehicles(vehicles, types), null);
    }

    /// <summary>
    /// Search load: show all active vehicles; mark period availability using Backend
    /// rentable set + busy-periods (2-hour buffer applied the same way as ScheduleConflictService).
    /// </summary>
    public static async Task<(IReadOnlyList<SearchVehicleCard> Results, string? Error)> LoadSearchAsync(
        CarRentalApiClient api,
        IReadOnlyList<VehicleTypeResponse> types,
        IReadOnlyList<int>? typeIds,
        IReadOnlyList<int>? seats,
        decimal? priceMax,
        DateTime? startDate,
        DateTime? endDate)
    {
        var ids = PositiveIds(typeIds);
        var seatMin = SeatMin(seats);

        var (all, allError) = await api.SearchVehiclesAsync(
            status: null,
            typeIds: ids,
            seats: seatMin,
            priceMax: priceMax,
            startDate: null,
            endDate: null);
        if (allError is not null)
            return ([], allError);

        var active = all
            .Where(v => !string.Equals(v.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            .ToList();

        HashSet<int> bookableIds;
        if (startDate is DateTime start && endDate is DateTime end)
        {
            var (bookable, bookError) = await api.SearchVehiclesAsync(
                "Available",
                ids,
                seatMin,
                priceMax,
                start,
                end);
            if (bookError is not null)
                return ([], bookError);
            bookableIds = bookable.Select(v => v.VehicleId).ToHashSet();
        }
        else
        {
            bookableIds = active
                .Where(v => string.Equals(v.Status, "Available", StringComparison.OrdinalIgnoreCase))
                .Select(v => v.VehicleId)
                .ToHashSet();
        }

        var catalog = CreateBookingUi.FromVehicles(active, types);
        var unavailable = catalog
            .Where(v => v.VehicleId is int id and > 0 && !bookableIds.Contains(id))
            .Select(v => (VehicleId: v.VehicleId!.Value, Vehicle: v))
            .ToList();

        var busyByVehicle = new Dictionary<int, IReadOnlyList<VehicleBusyPeriodResponse>>();
        if (startDate is DateTime selStart && endDate is DateTime selEnd && unavailable.Count > 0)
        {
            var from = selStart.AddHours(-TechnicalBuffer.TotalHours).AddDays(-1);
            var to = selEnd.AddDays(90);
            var tasks = unavailable.Select(async row =>
            {
                var result = await api.GetVehicleBusyPeriodsAsync(row.VehicleId, from, to);
                IReadOnlyList<VehicleBusyPeriodResponse> periods = result.Data ?? [];
                return (row.VehicleId, Periods: periods);
            });
            foreach (var row in await Task.WhenAll(tasks))
                busyByVehicle[row.VehicleId] = row.Periods;
        }

        var cards = new List<SearchVehicleCard>(catalog.Count);
        foreach (var vehicle in catalog)
        {
            var vehicleId = vehicle.VehicleId ?? 0;
            var isAvailable = vehicleId > 0 && bookableIds.Contains(vehicleId);
            DateTime? conflictStart = null;
            DateTime? conflictEnd = null;
            DateTime? availableFrom = null;

            if (!isAvailable
                && vehicleId > 0
                && startDate is DateTime wantStart
                && endDate is DateTime wantEnd
                && busyByVehicle.TryGetValue(vehicleId, out var periods))
            {
                var conflict = periods
                    .Where(p => OverlapsWithBuffer(wantStart, wantEnd, p.StartDate, p.EndDate))
                    .OrderBy(p => p.StartDate)
                    .ThenBy(p => p.EndDate)
                    .FirstOrDefault();
                if (conflict is not null)
                {
                    conflictStart = conflict.StartDate;
                    conflictEnd = conflict.EndDate;
                    availableFrom = ResolveNextAvailable(periods, wantStart, wantEnd);
                }
            }

            cards.Add(CreateBookingUi.ToSearchCard(
                vehicle,
                isAvailable,
                conflictStart,
                conflictEnd,
                availableFrom));
        }

        return (cards, null);
    }

    /// <summary>Same rule as Backend ScheduleConflictService.OverlapsWithBuffer.</summary>
    public static bool OverlapsWithBuffer(
        DateTime newStart, DateTime newEnd, DateTime existingStart, DateTime existingEnd)
        => newStart < existingEnd + TechnicalBuffer
           && newEnd > existingStart - TechnicalBuffer;

    public static DateTime? ResolveNextAvailable(
        IReadOnlyList<VehicleBusyPeriodResponse> periods,
        DateTime wantStart,
        DateTime wantEnd)
    {
        var ordered = periods.OrderBy(p => p.StartDate).ThenBy(p => p.EndDate).ToList();
        if (ordered.Count == 0)
            return null;

        var conflicting = ordered
            .Where(p => OverlapsWithBuffer(wantStart, wantEnd, p.StartDate, p.EndDate))
            .ToList();
        if (conflicting.Count == 0)
            return null;

        var duration = wantEnd - wantStart;
        if (duration <= TimeSpan.Zero)
            return null;

        var candidate = conflicting.Max(p => p.EndDate) + TechnicalBuffer;
        for (var i = 0; i < ordered.Count + 2; i++)
        {
            var block = ordered.FirstOrDefault(p =>
                OverlapsWithBuffer(candidate, candidate + duration, p.StartDate, p.EndDate));
            if (block is null)
                return candidate;
            candidate = block.EndDate + TechnicalBuffer;
        }

        return candidate;
    }

    public static string FormatPeriod(DateTime start, DateTime end)
        => $"{start:dd/MM HH:mm} → {end:dd/MM HH:mm}";

    public static string FormatMoment(DateTime value)
        => value.ToString("dd/MM HH:mm");
}
