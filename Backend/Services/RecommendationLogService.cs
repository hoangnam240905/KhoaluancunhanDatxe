using Backend.Data;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class RecommendationLogService(CarRentalDbContext db)
{
    /// <summary>Anti-spam window for optional VIEW interactions tied to recommendation views.</summary>
    public static readonly TimeSpan DefaultViewDedupeWindow = TimeSpan.FromMinutes(30);

    public async Task<(RecommendationLogResponse? Data, string? Error, int Status)> CreateAsync(
        CreateRecommendationLogRequest request)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == request.CustomerId))
            return (null, "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == request.VehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var row = new RecommendationLog
        {
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            Score = request.Score,
            RecommendationReason = TrimOrNull(request.RecommendationReason),
            CreatedAt = DateTime.UtcNow
        };
        db.RecommendationLogs.Add(row);
        await db.SaveChangesAsync();
        return (Map(row), null, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Step 4.5: persist one RecommendationLog per mapped vehicle after a successful V7 recommendation.
    /// Does not retrain models or alter V7 scores. Skips VehicleIds that do not exist.
    /// Optionally records VIEW once per customer+vehicle within <paramref name="viewDedupeWindow"/>.
    /// </summary>
    public async Task<int> LogSuccessfulRecommendationsAsync(
        int customerId,
        string? predictedSegment,
        IReadOnlyList<(int VehicleId, double Score)> items,
        DateTime? createdAt = null,
        bool alsoLogViewWithDedupe = true,
        TimeSpan? viewDedupeWindow = null,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return 0;
        if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId, cancellationToken))
            return 0;

        var vehicleIds = items.Select(i => i.VehicleId).Distinct().ToList();
        var existingIds = await db.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.VehicleId))
            .Select(v => v.VehicleId)
            .ToListAsync(cancellationToken);
        var existing = existingIds.ToHashSet();

        var at = createdAt ?? DateTime.UtcNow;
        var reason = BuildV7Reason(predictedSegment);
        var written = 0;

        foreach (var item in items)
        {
            if (!existing.Contains(item.VehicleId))
                continue;

            db.RecommendationLogs.Add(new RecommendationLog
            {
                CustomerId = customerId,
                VehicleId = item.VehicleId,
                Score = item.Score,
                RecommendationReason = reason,
                CreatedAt = at
            });
            written++;
        }

        if (alsoLogViewWithDedupe && written > 0)
        {
            var window = viewDedupeWindow ?? DefaultViewDedupeWindow;
            var since = at - window;
            var candidateIds = items.Where(i => existing.Contains(i.VehicleId))
                .Select(i => i.VehicleId)
                .Distinct()
                .ToList();

            var recentViews = await db.CustomerVehicleInteractions.AsNoTracking()
                .Where(i => i.CustomerId == customerId
                    && i.InteractionType == "VIEW"
                    && candidateIds.Contains(i.VehicleId)
                    && i.CreatedAt >= since)
                .Select(i => i.VehicleId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var alreadyViewed = recentViews.ToHashSet();

            foreach (var vehicleId in candidateIds)
            {
                if (alreadyViewed.Contains(vehicleId))
                    continue;
                db.CustomerVehicleInteractions.Add(new CustomerVehicleInteraction
                {
                    CustomerId = customerId,
                    VehicleId = vehicleId,
                    InteractionType = "VIEW",
                    CreatedAt = at
                });
            }
        }

        if (written > 0)
            await db.SaveChangesAsync(cancellationToken);

        return written;
    }

    public static string BuildV7Reason(string? predictedSegment)
    {
        var segment = string.IsNullOrWhiteSpace(predictedSegment)
            ? "unknown"
            : predictedSegment.Trim();
        return $"V7; predictedSegment={segment}";
    }

    public async Task<(List<RecommendationLogResponse>? Data, string? Error, int Status)> GetByCustomerAsync(
        int customerId)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId))
            return (null, "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);

        var rows = await db.RecommendationLogs.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.RecommendationId)
            .ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    public async Task<(List<RecommendationLogResponse>? Data, string? Error, int Status)> GetByVehicleAsync(
        int vehicleId)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
            return (null, "Không tìm thấy xe.", StatusCodes.Status404NotFound);

        var rows = await db.RecommendationLogs.AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.RecommendationId)
            .ToListAsync();
        return (rows.Select(Map).ToList(), null, StatusCodes.Status200OK);
    }

    private static RecommendationLogResponse Map(RecommendationLog r) => new(
        r.RecommendationId, r.CustomerId, r.VehicleId, r.Score, r.RecommendationReason, r.CreatedAt);

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
