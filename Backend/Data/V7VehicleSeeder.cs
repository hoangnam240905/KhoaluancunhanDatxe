using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Constants;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Data;

/// <summary>
/// Step 3.5: idempotent insert of a deterministic V7 car subset into Vehicles.
/// Does not delete/update existing fleet VehicleIds. Mapping is written to a JSON file (no schema change).
/// </summary>
public static class V7VehicleSeeder
{
    public const string CatalogFileName = "v7-vehicle-catalog.json";
    public const string MapFileName = "v7-carid-vehicle-map.json";
    public const string NotesPrefix = "v7:";
    public const string PlatePrefix = "V7-";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public sealed record SyncResult(
        int CatalogCount,
        int Inserted,
        int SkippedExisting,
        IReadOnlyDictionary<string, int> CarIdToVehicleId,
        string MapPath,
        IReadOnlyList<string> Warnings);

    public static string LicensePlateFor(string carId)
    {
        var digits = new string((carId ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
            digits = "0000";
        return $"{PlatePrefix}{digits.PadLeft(4, '0')}";
    }

    public static string NotesFor(string carId) => $"{NotesPrefix}{carId}";

    public static SyncResult Sync(
        CarRentalDbContext db,
        string dataDirectory,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        Directory.CreateDirectory(dataDirectory);

        var catalogPath = Path.Combine(dataDirectory, CatalogFileName);
        if (!File.Exists(catalogPath))
            throw new FileNotFoundException($"Missing V7 catalog: {catalogPath}");

        var catalogDoc = JsonSerializer.Deserialize<V7CatalogFile>(
            File.ReadAllText(catalogPath), JsonOptions)
            ?? throw new InvalidOperationException("Invalid V7 catalog JSON.");

        var vehicles = catalogDoc.Vehicles ?? [];
        if (vehicles.Count is < 30 or > 50)
            logger?.LogWarning(
                "V7 catalog size {Count} is outside preferred 30–50 range.", vehicles.Count);

        var existingPlates = db.Vehicles.AsNoTracking()
            .Select(v => v.LicensePlate)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byNotes = db.Vehicles.AsNoTracking()
            .Where(v => v.Notes != null && v.Notes.StartsWith(NotesPrefix))
            .ToDictionary(v => v.Notes!, v => v.VehicleId, StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var inserted = 0;
        var skipped = 0;
        var now = DateTime.UtcNow;

        // Preserve historical VehicleIds: never delete/update Vehicles  that already exist.
        var protectedIds = db.Vehicles.AsNoTracking().Select(v => v.VehicleId).ToHashSet();

        foreach (var item in vehicles)
        {
            if (string.IsNullOrWhiteSpace(item.CarId))
            {
                warnings.Add("Skipped catalog row with empty car_id.");
                continue;
            }

            var plate = LicensePlateFor(item.CarId);
            var notes = NotesFor(item.CarId);

            if (byNotes.TryGetValue(notes, out var existingByNotes))
            {
                map[item.CarId] = existingByNotes;
                skipped++;
                continue;
            }

            if (existingPlates.Contains(plate))
            {
                var existing = db.Vehicles.AsNoTracking()
                    .First(v => v.LicensePlate == plate);
                map[item.CarId] = existing.VehicleId;
                skipped++;
                continue;
            }

            var entity = new Vehicle
            {
                TypeId = ResolveTypeId(item),
                LicensePlate = plate,
                Brand = item.Make?.Trim() ?? "Unknown",
                Model = item.Model?.Trim() ?? item.CarId,
                Year = 2024,
                Color = null,
                Status = VehicleStatuses.Available,
                CurrentKm = 0,
                CreatedAt = now,
                Transmission = null,
                FuelType = string.IsNullOrWhiteSpace(item.FuelType) ? null : item.FuelType.Trim(),
                TrunkCapacity = item.TrunkL,
                FuelConsumption = item.KmPerL,
                SafetyScore = item.SafetyScore,
                ConditionStatus = "Good",
                Notes = notes
            };

            db.Vehicles.Add(entity);
            db.SaveChanges(); // get VehicleId for mapping immediately

            if (protectedIds.Contains(entity.VehicleId))
            {
                // Should never happen for new inserts; fail loud if identity collision.
                throw new InvalidOperationException(
                    $"V7 insert unexpectedly reused protected VehicleId {entity.VehicleId}.");
            }

            map[item.CarId] = entity.VehicleId;
            existingPlates.Add(plate);
            byNotes[notes] = entity.VehicleId;
            inserted++;
            logger?.LogInformation(
                "Seeded V7 {CarId} → VehicleId {VehicleId} ({Brand} {Model}, plate {Plate}).",
                item.CarId, entity.VehicleId, entity.Brand, entity.Model, plate);
        }

        // Validation: mapping integrity
        var mappedIds = map.Values.ToList();
        if (map.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != map.Count)
            warnings.Add("Duplicate car_id keys detected in mapping.");
        if (mappedIds.Distinct().Count() != mappedIds.Count)
            warnings.Add("Duplicate VehicleId values detected in mapping.");

        var existingIdSet = db.Vehicles.AsNoTracking().Select(v => v.VehicleId).ToHashSet();
        foreach (var (carId, vehicleId) in map)
        {
            if (!existingIdSet.Contains(vehicleId))
                warnings.Add($"Mapped VehicleId {vehicleId} for {carId} does not exist.");
        }

        var mapPath = Path.Combine(dataDirectory, MapFileName);
        var mapDoc = new V7MapFile
        {
            GeneratedAtUtc = DateTime.UtcNow,
            CatalogCount = vehicles.Count,
            Inserted = inserted,
            SkippedExisting = skipped,
            CarIdToVehicleId = map
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
        File.WriteAllText(mapPath, JsonSerializer.Serialize(mapDoc, JsonOptions));

        logger?.LogInformation(
            "V7 vehicle sync done: catalog={Catalog}, inserted={Inserted}, skipped={Skipped}, map={MapPath}",
            vehicles.Count, inserted, skipped, mapPath);

        return new SyncResult(vehicles.Count, inserted, skipped, map, mapPath, warnings);
    }

    /// <summary>Map V7 segment/seats onto existing VehicleTypes (no new types).</summary>
    internal static int ResolveTypeId(V7CatalogVehicle item)
    {
        var seats = item.Seats ?? 0;
        var segment = (item.Segment ?? string.Empty).Trim().ToLowerInvariant();

        if (segment is "suv" or "7-seater" or "crossover")
            return seats >= 8 ? 3 : 2;
        if (segment is "van" or "mpv")
            return 3;
        if (seats >= 10)
            return 3;
        if (seats >= 8)
            return 4;
        if (seats >= 6)
            return 2;
        return 1;
    }

    public static IReadOnlyDictionary<string, int> LoadMap(string dataDirectory)
    {
        var mapPath = Path.Combine(dataDirectory, MapFileName);
        if (!File.Exists(mapPath))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var doc = JsonSerializer.Deserialize<V7MapFile>(File.ReadAllText(mapPath), JsonOptions);
        return doc?.CarIdToVehicleId
               ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class V7CatalogFile
    {
        public List<V7CatalogVehicle>? Vehicles { get; set; }
    }

    public sealed class V7CatalogVehicle
    {
        [JsonPropertyName("car_id")]
        public string CarId { get; set; } = string.Empty;

        [JsonPropertyName("make")]
        public string? Make { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("segment")]
        public string? Segment { get; set; }

        [JsonPropertyName("fuel_type")]
        public string? FuelType { get; set; }

        [JsonPropertyName("seats")]
        public int? Seats { get; set; }

        [JsonPropertyName("trunk_l")]
        public double? TrunkL { get; set; }

        [JsonPropertyName("safety_score")]
        public double? SafetyScore { get; set; }

        [JsonPropertyName("km_per_l")]
        public double? KmPerL { get; set; }
    }

    private sealed class V7MapFile
    {
        public DateTime GeneratedAtUtc { get; set; }
        public int CatalogCount { get; set; }
        public int Inserted { get; set; }
        public int SkippedExisting { get; set; }
        public Dictionary<string, int> CarIdToVehicleId { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
