using Backend.Constants;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md (gợi ý xe) — Giai đoạn 2: budget on the V7 dataset scale + vehicle status in results.
/// </summary>
public class PhaseRecommendationStage2Tests
{
    private static readonly RecommendationServiceOptions Defaults = new();

    [Theory]
    [InlineData(1800, 1800)]          // already in dataset unit, inside range
    [InlineData(450, 900)]            // below dataset minimum -> clamped
    [InlineData(6000, 5315)]          // above dataset maximum -> clamped
    [InlineData(15_000_000, 15_000_000 / 5_600.0)] // VND/month -> dataset unit
    [InlineData(49_999_999, 5315)]    // VND outlier -> converted, then clamped
    [InlineData(100_000, 900)]        // threshold counts as VND (≈17.9) -> clamped up
    public void Budget_is_brought_onto_dataset_scale(double raw, double expected)
        => Assert.Equal(expected, CustomerV7ProfileBuilder.NormalizeBudget(raw, Defaults), precision: 6);

    [Fact]
    public void Outlier_vnd_budget_no_longer_reaches_python_raw()
    {
        var profile = CustomerV7ProfileBuilder.Build(
            new Customer { BudgetMax = 49_999_999 }, Defaults, new DateOnly(2026, 10, 18));

        Assert.Equal(5315, profile.BudgetMonthly);
    }

    [Fact]
    public void Missing_budget_still_uses_default()
    {
        var profile = CustomerV7ProfileBuilder.Build(new Customer(), Defaults, new DateOnly(2026, 10, 18));
        Assert.Equal(Defaults.DefaultBudgetMonthly, profile.BudgetMonthly);
    }

    [Fact]
    public async Task Mapped_recommendations_carry_current_vehicle_status_without_filtering()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Vehicles.Find(1)!.Status = VehicleStatuses.Available;   // Toyota Vios
        iso.Db.Vehicles.Find(2)!.Status = VehicleStatuses.Maintenance; // Hyundai Accent
        iso.Db.SaveChanges();

        var python = new FixedPython(
            ("car_a", "Toyota", "Vios", 0.9),
            ("car_b", "Hyundai", "Accent", 0.8));
        var service = new RecommendationMappingService(
            iso.Db,
            python,
            Microsoft.Extensions.Options.Options.Create(new RecommendationServiceOptions()),
            new V7CarIdVehicleMapStore(),
            NullLogger<RecommendationMappingService>.Instance);

        var result = await service.GetRecommendationsForCustomerAsync(3, topK: 5);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(2, result.Recommendations.Count); // maintenance car is still listed (hint, not a filter)
        Assert.Equal(VehicleStatuses.Available, result.Recommendations[0].VehicleStatus);
        Assert.Equal(VehicleStatuses.Maintenance, result.Recommendations[1].VehicleStatus);
    }

    private sealed class FixedPython(params (string CarId, string Make, string Model, double Score)[] rows)
        : IRecommendationService
    {
        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request, CancellationToken cancellationToken = default)
        {
            var items = rows
                .Select(r => new PythonRecommendationItem(CarId: r.CarId, Make: r.Make, Model: r.Model, Segment: "Sedan", FinalScore: r.Score))
                .ToList();
            return Task.FromResult<(PythonRecommendationResponse?, string?, int)>(
                (new PythonRecommendationResponse(true, "Sedan", items, 1), null, StatusCodes.Status200OK));
        }
    }
}
