using Backend.Constants;
using Backend.DTOs.Recommendations;
using Backend.Options;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md (form hồ sơ du lịch) — bước 3: seats-first ordering, large-party fleet additions, maintenance-due flag.
/// Seed fleet: 1 Vios, 2 Accent (4 chỗ), 3 Fortuner, 4 Everest (7 chỗ), 5 Solati (16 chỗ), 6 Transit (9 chỗ, Maintenance).
/// </summary>
public class PhaseTravelProfileStep3Tests
{
    private const int Customer = 3;

    [Fact]
    public async Task Cars_that_fit_the_party_rank_above_higher_scored_cars_that_do_not()
    {
        using var iso = Setup(partySize: 6);
        var python = new FixedPython(("car_vios", "Toyota", "Vios", 5, 0.9), ("car_fortuner", "Toyota", "Fortuner", 7, 0.8));

        var result = await Mapping(iso, python).GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.Equal(RecommendationMappingService.MaxPythonTopK, python.LastTopK); // larger candidate pool
        Assert.Equal(6, result.PartySize);
        Assert.Equal(["Fortuner", "Vios"], result.Recommendations.Select(r => r.Model));
        Assert.Equal([true, false], result.Recommendations.Select(r => r.SeatsOk == true));
        Assert.Equal(5, result.Recommendations[1].Seats); // catalog seats, not the coarser DB type
    }

    [Fact]
    public async Task Top_k_is_cut_after_seats_first_ordering()
    {
        using var iso = Setup(partySize: 6);
        var python = new FixedPython(("car_vios", "Toyota", "Vios", 5, 0.9), ("car_fortuner", "Toyota", "Fortuner", 7, 0.8));

        var result = await Mapping(iso, python).GetRecommendationsForCustomerAsync(Customer, topK: 1);

        Assert.Equal("Fortuner", Assert.Single(result.Recommendations).Model);
    }

    [Fact]
    public async Task Without_party_size_order_and_top_k_are_pure_v7()
    {
        using var iso = Setup(partySize: null);
        var python = new FixedPython(("car_vios", "Toyota", "Vios", 5, 0.9), ("car_fortuner", "Toyota", "Fortuner", 7, 0.8));

        var result = await Mapping(iso, python).GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.Equal(5, python.LastTopK);
        Assert.Null(result.PartySize);
        Assert.Equal(["Vios", "Fortuner"], result.Recommendations.Select(r => r.Model));
        Assert.All(result.Recommendations, r => Assert.Null(r.SeatsOk));
    }

    [Fact]
    public async Task Party_bigger_than_catalog_gets_fitting_fleet_vehicles_first()
    {
        using var iso = Setup(partySize: 12);
        var python = new FixedPython(("car_vios", "Toyota", "Vios", 5, 0.9), ("car_fortuner", "Toyota", "Fortuner", 7, 0.8));

        var result = await Mapping(iso, python).GetRecommendationsForCustomerAsync(Customer, topK: 5);

        var first = result.Recommendations[0];
        Assert.Equal(5, first.VehicleId); // Solati 16 chỗ; Transit 9 chỗ is too small (and in maintenance)
        Assert.True(first.FromBusinessRule);
        Assert.True(first.SeatsOk);
        Assert.Equal(16, first.Seats);
        Assert.All(result.Recommendations.Skip(1), r => Assert.False(r.SeatsOk));
        Assert.DoesNotContain(result.Recommendations, r => r.VehicleId == 6);
    }

    [Fact]
    public async Task No_vehicle_fits_a_very_large_party()
    {
        using var iso = Setup(partySize: 20);
        var python = new FixedPython(("car_vios", "Toyota", "Vios", 5, 0.9));

        var result = await Mapping(iso, python).GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.All(result.Recommendations, r => Assert.False(r.SeatsOk));
        Assert.DoesNotContain(result.Recommendations, r => r.FromBusinessRule);
    }

    [Fact]
    public async Task Maintenance_due_flag_uses_the_booking_lock_rule()
    {
        using var iso = Setup(partySize: null);
        iso.ClearMaintenanceRecords(1); // Vios: 45,000 km, never serviced -> due
        var python = new FixedPython(("car_vios", "Toyota", "Vios", 5, 0.9), ("car_fortuner", "Toyota", "Fortuner", 7, 0.8));

        var result = await Mapping(iso, python).GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.True(result.Recommendations.Single(r => r.Model == "Vios").MaintenanceDue);
        Assert.False(result.Recommendations.Single(r => r.Model == "Fortuner").MaintenanceDue);
    }

    // ---------- helpers ----------

    private static IsolatedCarRentalDb Setup(int? partySize)
    {
        var iso = new IsolatedCarRentalDb();
        iso.Db.Customers.Find(Customer)!.FamilySize = partySize;
        foreach (var id in new[] { 1, 2, 3, 4, 5 })
        {
            iso.Db.Vehicles.Find(id)!.Status = VehicleStatuses.Available;
            var km = iso.Db.Vehicles.Find(id)!.CurrentKm;
            iso.SetLastCompletedMaintenance(id, DateTime.UtcNow.AddDays(-10), km - 100);
        }
        iso.Db.SaveChanges();
        return iso;
    }

    private static RecommendationMappingService Mapping(IsolatedCarRentalDb iso, IRecommendationService python)
        => new(iso.Db, python,
            Microsoft.Extensions.Options.Options.Create(new RecommendationServiceOptions()),
            new V7CarIdVehicleMapStore(),
            NullLogger<RecommendationMappingService>.Instance);

    private sealed class FixedPython(params (string CarId, string Make, string Model, int Seats, double Score)[] rows)
        : IRecommendationService
    {
        public int LastTopK { get; private set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request, CancellationToken cancellationToken = default)
        {
            LastTopK = request.TopK;
            var items = rows.Select(r => new PythonRecommendationItem(
                CarId: r.CarId, Make: r.Make, Model: r.Model, Segment: "Sedan", Seats: r.Seats, FinalScore: r.Score)).ToList();
            return Task.FromResult<(PythonRecommendationResponse?, string?, int)>(
                (new PythonRecommendationResponse(true, "Sedan", items, 1), null, StatusCodes.Status200OK));
        }
    }
}
