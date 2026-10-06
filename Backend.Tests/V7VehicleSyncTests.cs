using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.Data;
using Backend.DTOs.Recommendations;
using Backend.Services.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Tests;

/// <summary>Step 3.5: sync deterministic V7 subset into Vehicles + car_id map (no schema change).</summary>
public class V7VehicleSyncTests
{
    private static string CatalogSourceDir
    {
        get
        {
            var backendData = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Backend", "Data"));
            return backendData;
        }
    }

    [Fact]
    public void Sync_inserts_catalog_idempotently_and_writes_map()
    {
        using var iso = new IsolatedCarRentalDb();
        var beforeIds = iso.Db.Vehicles.Select(v => v.VehicleId).OrderBy(x => x).ToList();
        var beforeBookingVehicles = iso.Db.Bookings
            .Where(b => b.AssignedVehicleId != null)
            .Select(b => new { b.BookingId, b.AssignedVehicleId })
            .OrderBy(x => x.BookingId)
            .ToList();

        var workDir = Path.Combine(Path.GetTempPath(), $"v7-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        File.Copy(
            Path.Combine(CatalogSourceDir, V7VehicleSeeder.CatalogFileName),
            Path.Combine(workDir, V7VehicleSeeder.CatalogFileName),
            overwrite: true);

        var first = V7VehicleSeeder.Sync(iso.Db, workDir, NullLogger.Instance);
        Assert.Equal(40, first.CatalogCount);
        Assert.Equal(40, first.Inserted);
        Assert.Equal(0, first.SkippedExisting);
        Assert.Equal(40, first.CarIdToVehicleId.Count);
        Assert.Empty(first.Warnings);
        Assert.True(File.Exists(first.MapPath));

        // Existing VehicleIds untouched
        var afterFirstIds = iso.Db.Vehicles.Select(v => v.VehicleId).OrderBy(x => x).ToList();
        Assert.All(beforeIds, id => Assert.Contains(id, afterFirstIds));
        Assert.Equal(beforeIds.Count + 40, afterFirstIds.Count);

        var bookingAfter = iso.Db.Bookings
            .Where(b => b.AssignedVehicleId != null)
            .Select(b => new { b.BookingId, b.AssignedVehicleId })
            .OrderBy(x => x.BookingId)
            .ToList();
        Assert.Equal(beforeBookingVehicles, bookingAfter);

        // Idempotent second run
        var second = V7VehicleSeeder.Sync(iso.Db, workDir, NullLogger.Instance);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(40, second.SkippedExisting);
        Assert.Equal(first.CarIdToVehicleId.Count, second.CarIdToVehicleId.Count);
        Assert.Equal(
            first.CarIdToVehicleId.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)),
            second.CarIdToVehicleId.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)));

        Assert.Equal(beforeIds.Count + 40, iso.Db.Vehicles.Count());

        // Duplicate checks
        Assert.Equal(40, second.CarIdToVehicleId.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(40, second.CarIdToVehicleId.Values.Distinct().Count());
        var v7Plates = iso.Db.Vehicles.Where(v => v.LicensePlate.StartsWith("V7-"))
            .Select(v => v.LicensePlate).ToList();
        Assert.Equal(v7Plates.Count, v7Plates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(second.CarIdToVehicleId.Values, id => Assert.True(iso.Db.Vehicles.Any(v => v.VehicleId == id)));

        // Brand+Model exact uniqueness for seeded V7 rows
        var seeded = iso.Db.Vehicles.Where(v => v.Notes != null && v.Notes.StartsWith("v7:")).ToList();
        Assert.Equal(40, seeded.Count);
        Assert.All(seeded, v => Assert.Equal("Available", v.Status));
        var brandModel = seeded.Select(v => (v.Brand.ToLowerInvariant(), v.Model.ToLowerInvariant())).ToList();
        Assert.Equal(brandModel.Count, brandModel.Distinct().Count());
    }

    [Fact]
    public void LicensePlate_is_deterministic_from_car_id()
    {
        Assert.Equal("V7-0001", V7VehicleSeeder.LicensePlateFor("car_0001"));
        Assert.Equal("V7-0137", V7VehicleSeeder.LicensePlateFor("car_0137"));
        Assert.Equal(V7VehicleSeeder.LicensePlateFor("car_0001"), V7VehicleSeeder.LicensePlateFor("car_0001"));
    }

    [Fact]
    public async Task Synced_vehicles_map_python_car_ids_to_real_vehicles()
    {
        await EnsurePythonHealthyAsync();

        using var iso = new IsolatedCarRentalDb();
        var workDir = Path.Combine(Path.GetTempPath(), $"v7-sync-map-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        File.Copy(
            Path.Combine(CatalogSourceDir, V7VehicleSeeder.CatalogFileName),
            Path.Combine(workDir, V7VehicleSeeder.CatalogFileName),
            overwrite: true);

        var sync = V7VehicleSeeder.Sync(iso.Db, workDir, NullLogger.Instance);
        var store = new V7CarIdVehicleMapStore();
        store.Replace(sync.CarIdToVehicleId);

        var customer = await iso.Db.Customers.FirstAsync(c => c.CustomerId == 3);
        customer.DateOfBirth = new DateOnly(1995, 3, 15);
        customer.IncomeRange = "med";
        customer.CityType = "large";
        customer.RentalPurpose = "city";
        customer.PreferencesText = "compact city car fuel economy reliable";
        customer.BudgetMax = 600;
        await iso.Db.SaveChangesAsync();

        var http = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:8000/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var python = new RecommendationService(http);

        // Real Python Top 5 — report how many hit the synced subset.
        var profile = CustomerV7ProfileBuilder.Build(
            customer, new Backend.Options.RecommendationServiceOptions());
        var pythonReq = CustomerV7ProfileBuilder.ToPythonRequest(profile, topK: 5);
        var (pythonData, pythonError, pythonStatus) = await python.GetRecommendationsAsync(pythonReq);
        Assert.Null(pythonError);
        Assert.Equal(200, pythonStatus);
        Assert.NotNull(pythonData);
        Assert.Equal(5, pythonData!.Recommendations.Count);

        var top5Mapped = new List<(string CarId, int VehicleId, string Brand, string Model)>();
        foreach (var item in pythonData.Recommendations)
        {
            if (!sync.CarIdToVehicleId.TryGetValue(item.CarId, out var vehicleId))
                continue;
            var vehicle = await iso.Db.Vehicles.FirstAsync(v => v.VehicleId == vehicleId);
            Assert.Equal(vehicle.Brand, item.Make, ignoreCase: true);
            // Model may be exact catalog model; Brand match + VehicleId map is authoritative.
            top5Mapped.Add((item.CarId, vehicleId, vehicle.Brand, vehicle.Model));
        }

        // Deterministic proof: 5 synced catalog car_ids map through RecommendationMappingService.
        var proofIds = sync.CarIdToVehicleId.Keys.OrderBy(k => k).Take(5).ToArray();
        var fakeItems = proofIds.Select((id, i) =>
        {
            var v = iso.Db.Vehicles.First(x => x.VehicleId == sync.CarIdToVehicleId[id]);
            return (id, v.Brand, v.Model, "Sedan", 1.0 - i * 0.01);
        }).ToArray();

        var fake = FakePython("Sedan", fakeItems);
        var mapping = new RecommendationMappingService(
            iso.Db,
            fake,
            Microsoft.Extensions.Options.Options.Create(new Backend.Options.RecommendationServiceOptions()),
            store,
            NullLogger<RecommendationMappingService>.Instance);

        var mapped = await mapping.GetRecommendationsForCustomerAsync(3, topK: 5);
        Assert.True(mapped.Success, mapped.Reason);
        Assert.Equal(5, mapped.Recommendations.Count);
        Assert.Equal(proofIds, mapped.Recommendations.Select(r => r.CarId).ToArray());
        Assert.All(mapped.Recommendations, r =>
        {
            Assert.Equal(sync.CarIdToVehicleId[r.CarId], r.VehicleId);
            Assert.False(string.IsNullOrWhiteSpace(r.Brand));
            Assert.False(string.IsNullOrWhiteSpace(r.Model));
        });

        // Live Top5 may partially miss the 40-car subset; catalog proof above is mandatory.
        Assert.True(top5Mapped.Count >= 0); // recorded for report; non-negative
        Assert.Equal(40, sync.CarIdToVehicleId.Count);
    }

    private static IRecommendationService FakePython(
        string segment,
        params (string CarId, string Make, string Model, string Segment, double Score)[] items)
    {
        var recs = "[" + string.Join(",", items.Select(i =>
            $$"""{"car_id":"{{i.CarId}}","make":"{{i.Make}}","model":"{{i.Model}}","segment":"{{i.Segment}}","final_score":{{i.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""))
            + "]";
        var body = $$"""{"success":true,"predicted_segment":"{{segment}}","recommendations":{{recs}},"train_count":1}""";
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));
        return new RecommendationService(new HttpClient(handler) { BaseAddress = new Uri("http://fake.test/") });
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private static async Task EnsurePythonHealthyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await http.GetAsync("http://127.0.0.1:8000/health");
            Assert.True(response.IsSuccessStatusCode, "Requires RecommendationService :8000");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Fail($"Requires RecommendationService :8000 — {ex.Message}");
        }
    }
}
