using System.Net;
using System.Text;
using System.Text.Json;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Step 3: Customer/Vehicle SQLite → V7 profile → Python → car_id → Vehicle mapping.
/// Does not INSERT RecommendationLogs / interactions; does not UPDATE Customer/Vehicle.
/// </summary>
public class RecommendationMappingTests
{
    private const string BaseUrl = "http://127.0.0.1:8000";
    private static readonly DateOnly FixedToday = new(2026, 10, 6);

    private static RecommendationServiceOptions DefaultOptions() => new()
    {
        BaseUrl = BaseUrl,
        TimeoutSeconds = 30,
        DefaultChargingAccess = 0,
        DefaultBudgetMonthly = 2000,
        DefaultAge = 30,
        DefaultFamilySize = 2,
        DefaultPreferencesText = "general car rental preferences"
    };

    private static RecommendationMappingService CreateMappingService(
        IsolatedCarRentalDb iso,
        IRecommendationService python,
        RecommendationServiceOptions? opts = null)
        => new(
            iso.Db,
            python,
            Microsoft.Extensions.Options.Options.Create(opts ?? DefaultOptions()),
            new V7CarIdVehicleMapStore(),
            NullLogger<RecommendationMappingService>.Instance);

    private static Customer FullProfileCustomer(int customerId = 3) => new()
    {
        CustomerId = customerId,
        DateOfBirth = new DateOnly(1995, 3, 15),
        FamilySize = 4,
        NumberOfChildren = 2,
        IncomeRange = "high",
        CityType = "urban",
        CommuteKmPerDay = 25.5,
        AnnualMileage = 15000,
        RentalPurpose = "family",
        BudgetMin = 400,
        BudgetMax = 900,
        PreferencesText = "spacious family SUV with high safety",
        PreferredVehicleType = "SUV",
        PreferredFuelType = "Hybrid",
        PreferredTransmission = "Automatic",
        PreferredSeats = 7
    };

    private static async Task EnsurePythonHealthyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await http.GetAsync($"{BaseUrl}/health");
            Assert.True(
                response.IsSuccessStatusCode,
                $"Integration test requires RecommendationService at {BaseUrl}/health. Start: python -m uvicorn app:app --host 127.0.0.1 --port 8000");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Fail($"Integration test requires RecommendationService at {BaseUrl}. Underlying: {ex.Message}");
        }
    }

    private static IRecommendationService LivePythonClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        return new RecommendationService(http);
    }

    // ---------- TEST 1–4: profile mapping (no Python) ----------

    [Fact]
    public void Test1_Customer_with_profile_builds_v7_profile()
    {
        var profile = CustomerV7ProfileBuilder.Build(FullProfileCustomer(), DefaultOptions(), FixedToday);

        Assert.Equal(31, profile.Age); // 1995-03-15 → 2026-10-06
        Assert.Equal(2, profile.Kids);
        Assert.Equal(4, profile.FamilySize);
        Assert.Equal("high", profile.IncomeBracket);
        Assert.Equal("large", profile.CityType); // urban → large
        Assert.Equal(25.5, profile.CommuteKmDay);
        Assert.Equal(0, profile.ChargingAccess);
        Assert.Equal(15000, profile.AnnualKm);
        Assert.Equal("family", profile.UsagePrimary);
        Assert.Equal(900, profile.BudgetMonthly);
        Assert.StartsWith("spacious family SUV with high safety | ", profile.PreferencesText); // note kept, dropdown tokens appended
    }

    [Fact]
    public void Test2_Age_computed_from_DateOfBirth_not_hardcoded()
    {
        var opts = DefaultOptions();
        var c1 = new Customer { DateOfBirth = new DateOnly(2000, 1, 1) };
        var c2 = new Customer { DateOfBirth = new DateOnly(1980, 12, 31) };

        var a1 = CustomerV7ProfileBuilder.Build(c1, opts, FixedToday).Age;
        var a2 = CustomerV7ProfileBuilder.Build(c2, opts, FixedToday).Age;

        Assert.Equal(26, a1);
        Assert.Equal(45, a2);
        Assert.NotEqual(a1, a2);
        Assert.NotEqual(opts.DefaultAge, a1);
        Assert.NotEqual(opts.DefaultAge, a2);
    }

    [Fact]
    public void Test3_All_priority_fields_map_to_v7()
    {
        var customer = FullProfileCustomer();
        var profile = CustomerV7ProfileBuilder.Build(customer, DefaultOptions(), FixedToday);

        Assert.Equal(
            CustomerV7ProfileBuilder.ComputeAge(customer.DateOfBirth, FixedToday, 30),
            profile.Age);
        Assert.Equal(customer.NumberOfChildren, profile.Kids);
        Assert.Equal(customer.FamilySize, profile.FamilySize);
        Assert.Equal("high", profile.IncomeBracket);
        Assert.Equal("large", profile.CityType);
        Assert.Equal(customer.CommuteKmPerDay!.Value, profile.CommuteKmDay);
        Assert.Equal((double)customer.AnnualMileage!.Value, profile.AnnualKm);
        Assert.Equal("family", profile.UsagePrimary);
        Assert.Equal(customer.BudgetMax!.Value, profile.BudgetMonthly);
        Assert.StartsWith(customer.PreferencesText!, profile.PreferencesText);
    }

    [Fact]
    public void Test4_Missing_optional_fields_use_fallbacks_without_crash()
    {
        var customer = new Customer { CustomerId = 99 }; // all optional null
        var opts = DefaultOptions();

        var profile = CustomerV7ProfileBuilder.Build(customer, opts, FixedToday);

        Assert.Equal(opts.DefaultAge, profile.Age);
        Assert.Equal(0, profile.Kids);
        Assert.Equal(opts.DefaultFamilySize, profile.FamilySize);
        Assert.Equal("med", profile.IncomeBracket);
        Assert.Equal("med", profile.CityType);
        Assert.Equal(opts.DefaultCommuteKmDay, profile.CommuteKmDay);
        Assert.Equal(0, profile.ChargingAccess);
        Assert.Equal(opts.DefaultAnnualKm, profile.AnnualKm);
        Assert.Equal(opts.DefaultUsagePrimary, profile.UsagePrimary);
        Assert.Equal(opts.DefaultBudgetMonthly, profile.BudgetMonthly);
        Assert.Equal("crossover safety", profile.PreferencesText); // no note/preferences -> trip type decides
    }

    [Fact]
    public void Budget_prefers_Max_then_Min_then_fallback()
    {
        var opts = DefaultOptions();
        Assert.Equal(800, CustomerV7ProfileBuilder.ResolveBudget(
            new Customer { BudgetMax = 800, BudgetMin = 100 }, opts.DefaultBudgetMonthly));
        Assert.Equal(100, CustomerV7ProfileBuilder.ResolveBudget(
            new Customer { BudgetMin = 100 }, opts.DefaultBudgetMonthly));
        Assert.Equal(2000, CustomerV7ProfileBuilder.ResolveBudget(
            new Customer(), opts.DefaultBudgetMonthly));
    }

    // ---------- TEST 5: live Python ----------

    [Fact]
    public async Task Test5_Backend_calls_live_Python_for_real_customer()
    {
        await EnsurePythonHealthyAsync();
        using var iso = new IsolatedCarRentalDb();

        var customer = await iso.Db.Customers.FirstAsync(c => c.CustomerId == 3);
        customer.DateOfBirth = new DateOnly(1995, 3, 15);
        customer.FamilySize = 4;
        customer.NumberOfChildren = 2;
        customer.IncomeRange = "high";
        customer.CityType = "large";
        customer.CommuteKmPerDay = 20;
        customer.AnnualMileage = 12000;
        customer.RentalPurpose = "family";
        customer.BudgetMax = 900;
        customer.PreferencesText = "family suv safety spacious trunk";
        await iso.Db.SaveChangesAsync();

        // Seed fleet vehicles that can match Python make/model for mapping coverage in this isolated DB.
        SeedMatchingFleet(iso);

        var svc = CreateMappingService(iso, LivePythonClient());
        var result = await svc.GetRecommendationsForCustomerAsync(3, topK: 5);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(200, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.PredictedSegment));
        // Mapped + dropped must account for Python topK (unmapped / ambiguous / VehicleId dedupe).
        Assert.Equal(
            5,
            result.Recommendations.Count
            + result.UnmappedCount
            + result.AmbiguousCount
            + result.DuplicateVehicleCount);
        Assert.Equal(
            result.Recommendations.Count,
            result.Recommendations.Select(r => r.VehicleId).Distinct().Count());
        Assert.True(
            result.Recommendations.Count > 0,
            $"Expected at least one Brand+Model map with seeded V7-like fleet. Reason={result.Reason}");
        foreach (var item in result.Recommendations)
        {
            Assert.True(item.VehicleId > 0);
            Assert.False(string.IsNullOrWhiteSpace(item.CarId));
            Assert.False(string.IsNullOrWhiteSpace(item.Brand));
            Assert.False(string.IsNullOrWhiteSpace(item.Model));
            // Authoritative vehicle data from SQLite, not Python make/model passthrough only.
            Assert.False(string.IsNullOrWhiteSpace(item.LicensePlate));
        }
    }

    // ---------- TEST 6–9: car_id mapping ----------

    [Fact]
    public async Task Test6_Valid_car_id_maps_to_real_Vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var v = SeedExactVehicle(iso, "Toyota", "Corolla Urban", "51Z-MAP01");

        var python = FakePython(
            segment: "Sedan",
            ("car_0099", "Toyota", "Corolla Urban", "Sedan", 0.91));

        var svc = CreateMappingService(iso, python);
        await EnsureCustomerProfileAsync(iso, 3);
        var result = await svc.GetRecommendationsForCustomerAsync(3, 5);

        Assert.True(result.Success);
        Assert.Single(result.Recommendations);
        Assert.Equal(v.VehicleId, result.Recommendations[0].VehicleId);
        Assert.Equal("car_0099", result.Recommendations[0].CarId);
        Assert.Equal("Toyota", result.Recommendations[0].Brand);
        Assert.Equal("Corolla Urban", result.Recommendations[0].Model);
        Assert.Equal(0.91, result.Recommendations[0].Score);
        Assert.Equal(0, result.UnmappedCount);
    }

    [Fact]
    public async Task Test7_Unknown_car_id_dropped_with_warning_no_crash()
    {
        using var iso = new IsolatedCarRentalDb();
        SeedExactVehicle(iso, "Toyota", "Corolla Urban", "51Z-MAP01");

        var python = FakePython(
            segment: "Sedan",
            ("car_ghost", "UnknownMake", "UnknownModel", "Sedan", 0.5),
            ("car_0099", "Toyota", "Corolla Urban", "Sedan", 0.4));

        var svc = CreateMappingService(iso, python);
        await EnsureCustomerProfileAsync(iso, 3);
        var result = await svc.GetRecommendationsForCustomerAsync(3, 5);

        Assert.True(result.Success);
        Assert.Equal(1, result.UnmappedCount);
        Assert.Single(result.Recommendations);
        Assert.Equal("car_0099", result.Recommendations[0].CarId);
    }

    [Fact]
    public async Task Test8_Duplicate_Brand_Model_is_ambiguous_not_random()
    {
        using var iso = new IsolatedCarRentalDb();
        SeedExactVehicle(iso, "Toyota", "Corolla", "51Z-DUP1");
        SeedExactVehicle(iso, "Toyota", "Corolla", "51Z-DUP2");

        var python = FakePython(
            segment: "Sedan",
            ("car_dup", "Toyota", "Corolla", "Sedan", 0.88));

        var svc = CreateMappingService(iso, python);
        await EnsureCustomerProfileAsync(iso, 3);
        var result = await svc.GetRecommendationsForCustomerAsync(3, 5);

        Assert.True(result.Success);
        Assert.Empty(result.Recommendations);
        Assert.Equal(1, result.AmbiguousCount);
        Assert.Contains("map", result.Reason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test9_TopK_preserves_Python_ranking_after_mapping()
    {
        using var iso = new IsolatedCarRentalDb();
        var a = SeedExactVehicle(iso, "Toyota", "Yaris Urban", "51Z-R1");
        var b = SeedExactVehicle(iso, "Hyundai", "Tucson Limited", "51Z-R2");
        var c = SeedExactVehicle(iso, "Toyota", "RAV4 Sport", "51Z-R3");

        var python = FakePython(
            segment: "SUV",
            ("car_a", "Toyota", "Yaris Urban", "Hatchback", 0.99),
            ("car_skip", "NoBrand", "NoModel", "X", 0.98),
            ("car_b", "Hyundai", "Tucson Limited", "SUV", 0.97),
            ("car_c", "Toyota", "RAV4 Sport", "SUV", 0.96));

        var svc = CreateMappingService(iso, python);
        await EnsureCustomerProfileAsync(iso, 3);
        var result = await svc.GetRecommendationsForCustomerAsync(3, topK: 5);

        Assert.Equal(3, result.Recommendations.Count);
        Assert.Equal(new[] { a.VehicleId, b.VehicleId, c.VehicleId },
            result.Recommendations.Select(r => r.VehicleId).ToArray());
        Assert.Equal(new[] { "car_a", "car_b", "car_c" },
            result.Recommendations.Select(r => r.CarId).ToArray());
        // Scores must stay Python order / values — not re-sorted by VehicleId
        Assert.True(result.Recommendations[0].Score > result.Recommendations[1].Score);
        Assert.True(result.Recommendations[1].Score > result.Recommendations[2].Score);
    }

    // ---------- TEST 10: topK validation ----------

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Test10_TopK_out_of_range_fails_validation(int topK)
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = CreateMappingService(iso, FakePython("Sedan"));

        var result = await svc.GetRecommendationsForCustomerAsync(3, topK);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("topK", result.Reason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_customer_returns_404()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = CreateMappingService(iso, FakePython("Sedan"));

        var result = await svc.GetRecommendationsForCustomerAsync(99999, 5);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Không tìm thấy khách hàng.", result.Reason);
    }

    [Fact]
    public void Mapper_does_not_assume_car_number_equals_VehicleId()
    {
        var vehicles = new List<Vehicle>
        {
            new() { VehicleId = 1, Brand = "Toyota", Model = "Vios", LicensePlate = "A" },
            new() { VehicleId = 7, Brand = "Skoda", Model = "Octavia Comfort", LicensePlate = "B" }
        };

        var map = VehicleCarIdMapper.MapRecommendations(
            [("car_0001", "Skoda", "Octavia Comfort", "Sedan", 0.5)],
            vehicles,
            explicitCarIdToVehicleId: null);

        Assert.Single(map.Mapped);
        Assert.Equal(7, map.Mapped[0].VehicleId); // NOT VehicleId 1 from car_0001
    }

    [Fact]
    public void Prefix_model_match_unique_ok_ambiguous_rejected()
    {
        var unique = new List<Vehicle>
        {
            new() { VehicleId = 10, Brand = "Toyota", Model = "Corolla", LicensePlate = "X" }
        };
        var hit = VehicleCarIdMapper.FindBrandModelMatches("Toyota", "Corolla Urban", unique);
        Assert.Single(hit);

        var dups = new List<Vehicle>
        {
            new() { VehicleId = 10, Brand = "Toyota", Model = "Corolla", LicensePlate = "X" },
            new() { VehicleId = 11, Brand = "Toyota", Model = "Corolla", LicensePlate = "Y" }
        };
        var amb = VehicleCarIdMapper.FindBrandModelMatches("Toyota", "Corolla Urban", dups);
        Assert.Equal(2, amb.Count);
    }

    // ---------- helpers ----------

    private static async Task EnsureCustomerProfileAsync(IsolatedCarRentalDb iso, int customerId)
    {
        var c = await iso.Db.Customers.FirstAsync(x => x.CustomerId == customerId);
        c.DateOfBirth ??= new DateOnly(1995, 3, 15);
        c.PreferencesText ??= "city car fuel economy";
        c.IncomeRange ??= "med";
        c.CityType ??= "med";
        c.RentalPurpose ??= "city";
        await iso.Db.SaveChangesAsync();
    }

    private static Vehicle SeedExactVehicle(
        IsolatedCarRentalDb iso, string brand, string model, string plate)
    {
        var v = new Vehicle
        {
            TypeId = 1,
            Brand = brand,
            Model = model,
            LicensePlate = plate,
            Year = 2024,
            Status = "Available",
            CreatedAt = DateTime.UtcNow
        };
        iso.Db.Vehicles.Add(v);
        iso.Db.SaveChanges();
        return v;
    }

    /// <summary>
    /// Isolated-test fleet that overlaps common V7 make/model prefixes so live Python calls can map.
    /// Does not modify production carrental.db.
    /// </summary>
    private static void SeedMatchingFleet(IsolatedCarRentalDb iso)
    {
        // Base models from Mayab2/carfit-ai-synthetic cars (prefix-match trim variants).
        foreach (var (brand, model, plate) in new[]
                 {
                     ("Kia", "Sorento", "51K-SOR01"),
                     ("Kia", "Picanto", "51K-PIC01"),
                     ("Kia", "Sportage", "51K-SPO01"),
                     ("Skoda", "Octavia", "51S-OCT01"),
                     ("Skoda", "Kodiaq", "51S-KOD01"),
                     ("MINI", "Cooper 3-Door", "51M-COP01"),
                     ("Fiat", "500", "51F-50001"),
                     ("Porsche", "Macan", "51P-MAC01"),
                     ("Peugeot", "5008", "51P-5008"),
                     ("Toyota", "Corolla", "51T-COR01"),
                     ("Toyota", "Yaris", "51T-YAR01"),
                     ("Toyota", "RAV4", "51T-RAV01"),
                     ("Hyundai", "Tucson", "51H-TUC01"),
                     ("Hyundai", "i20", "51H-I2001"),
                     ("Mazda", "3", "51M-30001"),
                     ("Mazda", "CX-5", "51M-CX501"),
                     ("Nissan", "Qashqai", "51N-QAS01"),
                     ("Nissan", "X-Trail", "51N-XTR01"),
                     ("Jeep", "Wrangler", "51J-WRA01"),
                     ("Smart", "Fortwo", "51S-FOR01")
                 })
        {
            if (!iso.Db.Vehicles.Any(v => v.LicensePlate == plate))
                SeedExactVehicle(iso, brand, model, plate);
        }
    }

    private static IRecommendationService FakePython(
        string segment,
        params (string CarId, string Make, string Model, string Segment, double Score)[] items)
    {
        var recs = items.Length == 0
            ? """[{"car_id":"car_0001","make":"Skoda","model":"Octavia Comfort","segment":"Sedan","final_score":0.4}]"""
            : "[" + string.Join(",", items.Select(i =>
                $$"""{"car_id":"{{i.CarId}}","make":"{{i.Make}}","model":"{{i.Model}}","segment":"{{i.Segment}}","final_score":{{i.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""))
              + "]";

        var body = $$"""
            {"success":true,"predicted_segment":"{{segment}}","recommendations":{{recs}},"train_count":1}
            """;

        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://fake.test/") };
        return new RecommendationService(http);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
