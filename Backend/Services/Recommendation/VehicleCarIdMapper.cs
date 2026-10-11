using System.Text.RegularExpressions;
using Backend.Entities;
using Microsoft.Extensions.Logging;

namespace Backend.Services.Recommendation;

/// <summary>
/// Maps Python V7 car_id (+ make/model) to SQLite Vehicles.
/// Never assumes car_NNNN == VehicleId N. Never picks randomly on duplicates.
/// </summary>
public static partial class VehicleCarIdMapper
{
    public sealed record MapOutcome(
        IReadOnlyList<MappedVehicle> Mapped,
        int UnmappedCount,
        int AmbiguousCount);

    public sealed record MappedVehicle(
        string CarId,
        int VehicleId,
        Vehicle Vehicle,
        string Segment,
        double Score);

    public static MapOutcome MapRecommendations(
        IReadOnlyList<(string CarId, string Make, string Model, string Segment, double Score)> pythonItems,
        IReadOnlyList<Vehicle> vehicles,
        IReadOnlyDictionary<string, int>? explicitCarIdToVehicleId,
        ILogger? logger = null)
    {
        var mapped = new List<MappedVehicle>();
        var unmapped = 0;
        var ambiguous = 0;
        var byId = vehicles.ToDictionary(v => v.VehicleId);

        foreach (var item in pythonItems)
        {
            if (string.IsNullOrWhiteSpace(item.CarId))
            {
                unmapped++;
                logger?.LogWarning("Recommendation car_id empty — skipped.");
                continue;
            }

            if (explicitCarIdToVehicleId is not null
                && explicitCarIdToVehicleId.TryGetValue(item.CarId, out var explicitId))
            {
                if (byId.TryGetValue(explicitId, out var explicitVehicle))
                {
                    mapped.Add(new MappedVehicle(
                        item.CarId, explicitId, explicitVehicle, item.Segment, item.Score));
                    continue;
                }

                unmapped++;
                logger?.LogWarning(
                    "Explicit car_id {CarId} → VehicleId {VehicleId} not found in SQLite — skipped.",
                    item.CarId, explicitId);
                continue;
            }

            var matches = FindBrandModelMatches(item.Make, item.Model, vehicles);
            if (matches.Count == 1)
            {
                var v = matches[0];
                mapped.Add(new MappedVehicle(item.CarId, v.VehicleId, v, item.Segment, item.Score));
                continue;
            }

            if (matches.Count > 1)
            {
                ambiguous++;
                logger?.LogWarning(
                    "Ambiguous Brand+Model for car_id {CarId} ({Make} {Model}): {Count} vehicles — skipped (no random pick).",
                    item.CarId, item.Make, item.Model, matches.Count);
                continue;
            }

            unmapped++;
            logger?.LogWarning(
                "No SQLite Vehicle for car_id {CarId} ({Make} {Model}) — skipped.",
                item.CarId, item.Make, item.Model);
        }

        return new MapOutcome(mapped, unmapped, ambiguous);
    }

    /// <summary>
    /// Exact normalized Brand+Model, else unique Brand + Vehicle.Model prefix of Python model
    /// (handles "Corolla Urban" ↔ vehicle Model "Corolla"). Multiple hits → empty (caller treats as ambiguous).
    /// </summary>
    public static IReadOnlyList<Vehicle> FindBrandModelMatches(
        string? make, string? model, IReadOnlyList<Vehicle> vehicles)
    {
        var brand = NormalizeKey(make);
        var pythonModel = NormalizeKey(model);
        if (string.IsNullOrEmpty(brand) || string.IsNullOrEmpty(pythonModel))
            return [];

        var exact = vehicles
            .Where(v => NormalizeKey(v.Brand) == brand && NormalizeKey(v.Model) == pythonModel)
            .ToList();
        if (exact.Count > 0)
            return exact;

        var prefix = vehicles
            .Where(v =>
            {
                if (NormalizeKey(v.Brand) != brand) return false;
                var vm = NormalizeKey(v.Model);
                if (string.IsNullOrEmpty(vm)) return false;
                return pythonModel == vm
                       || pythonModel.StartsWith(vm + " ", StringComparison.Ordinal);
            })
            .ToList();

        return prefix;
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var collapsed = WhitespaceRegex().Replace(value.Trim(), " ");
        return collapsed.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
