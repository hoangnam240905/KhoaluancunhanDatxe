using Backend.DTOs.Customers;
using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;
using Backend.Services;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md (form hồ sơ du lịch) — bước 2: 4-field profile, per-day trip budget, "Vượt ngân sách" flag.
/// </summary>
public class PhaseTravelProfileStep2Tests
{
    private const int Customer = 3;

    private static UpdateCustomerProfileRequest TravelProfile(double? budgetPerDay, string? tripType = "mountain_trip")
        => new(
            DateOfBirth: null, NumberOfChildren: null, FamilySize: 6, IncomeRange: null, CityType: null,
            CommuteKmPerDay: null, AnnualMileage: null, RentalPurpose: tripType, BudgetMin: null, BudgetMax: null,
            PreferencesText: "xe rộng rãi, an toàn", TripBudgetPerDay: budgetPerDay);

    [Fact]
    public async Task Profile_saves_trip_type_and_per_day_budget()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new CustomerProfileService(iso.Db);

        var (data, error, status) = await service.UpdateOwnAsync(Customer, TravelProfile(1_200_000));

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.Equal(1_200_000, data!.TripBudgetPerDay);
        Assert.Equal("mountain_trip", data.RentalPurpose);
        Assert.Equal(6, data.FamilySize);
        Assert.Equal(1_200_000, iso.Db.Customers.Find(Customer)!.TripBudgetPerDay);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000_001)]
    public async Task Invalid_per_day_budget_is_rejected(double budget)
    {
        using var iso = new IsolatedCarRentalDb();
        var (_, error, status) = await new CustomerProfileService(iso.Db).UpdateOwnAsync(Customer, TravelProfile(budget));
        Assert.Equal(400, status);
        Assert.NotNull(error);
    }

    [Fact]
    public void Per_day_budget_customer_sends_training_median_budget_to_v7()
    {
        var options = new RecommendationServiceOptions();
        var withTripBudget = new Customer { TripBudgetPerDay = 900_000, BudgetMax = 49_999_999 };
        var legacy = new Customer { BudgetMax = 1_800 };

        Assert.Equal(2276, CustomerV7ProfileBuilder.Build(withTripBudget, options).BudgetMonthly);
        Assert.Equal(1_800, CustomerV7ProfileBuilder.Build(legacy, options).BudgetMonthly); // old rows unchanged
    }

    [Fact]
    public async Task Over_budget_flag_compares_real_rental_price_without_filtering()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Customers.Find(Customer)!.TripBudgetPerDay = 1_000_000;
        iso.Db.SaveChanges();
        var vios = iso.Db.VehicleTypes.Find(iso.Db.Vehicles.Find(1)!.TypeId)!;       // 4 chỗ
        var fortuner = iso.Db.VehicleTypes.Find(iso.Db.Vehicles.Find(3)!.TypeId)!;   // 7 chỗ
        Assert.True(vios.PricePerDay <= 1_000_000 && fortuner.PricePerDay > 1_000_000);

        var result = await Mapping(iso, ("car_a", "Toyota", "Vios"), ("car_b", "Toyota", "Fortuner"))
            .GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(2, result.Recommendations.Count);
        Assert.Equal((vios.PricePerDay, (bool?)false), (result.Recommendations[0].PricePerDay, result.Recommendations[0].OverBudget));
        Assert.Equal((fortuner.PricePerDay, (bool?)true), (result.Recommendations[1].PricePerDay, result.Recommendations[1].OverBudget));
    }

    [Fact]
    public async Task No_budget_means_no_over_budget_flag()
    {
        using var iso = new IsolatedCarRentalDb();
        var result = await Mapping(iso, ("car_a", "Toyota", "Vios")).GetRecommendationsForCustomerAsync(Customer, topK: 5);
        Assert.Null(Assert.Single(result.Recommendations).OverBudget);
    }

    private static RecommendationMappingService Mapping(IsolatedCarRentalDb iso, params (string CarId, string Make, string Model)[] rows)
        => new(iso.Db, new FixedPython(rows),
            Microsoft.Extensions.Options.Options.Create(new RecommendationServiceOptions()),
            new V7CarIdVehicleMapStore(),
            NullLogger<RecommendationMappingService>.Instance);

    private sealed class FixedPython((string CarId, string Make, string Model)[] rows) : IRecommendationService
    {
        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request, CancellationToken cancellationToken = default)
        {
            var items = rows.Select((r, i) => new PythonRecommendationItem(
                CarId: r.CarId, Make: r.Make, Model: r.Model, Segment: "Sedan", FinalScore: 0.9 - i * 0.1)).ToList();
            return Task.FromResult<(PythonRecommendationResponse?, string?, int)>(
                (new PythonRecommendationResponse(true, "Sedan", items, 1), null, StatusCodes.Status200OK));
        }
    }
}
