using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;
using Backend.Services.Recommendation;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md (form hồ sơ du lịch) — bước 1: median defaults, trip-type mapping, Vietnamese text → V7 vocabulary.
/// </summary>
public class PhaseTravelProfileStep1Tests
{
    private static readonly RecommendationServiceOptions Defaults = new();
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static CustomerV7Profile Build(Customer c) => CustomerV7ProfileBuilder.Build(c, Defaults, Today);

    [Fact]
    public void Missing_commuter_fields_use_training_medians_not_zero()
    {
        var p = Build(new Customer());
        Assert.Equal(34, p.Age);
        Assert.Equal(22, p.CommuteKmDay);
        Assert.Equal(14_237.5, p.AnnualKm);
        Assert.Equal("family", p.UsagePrimary);
    }

    [Theory]
    [InlineData("family_trip", "family")]
    [InlineData("mountain_trip", "trips")]
    [InlineData("business_trip", "work")]
    [InlineData("city_trip", "city")]
    [InlineData("trips", "trips")]   // old values still accepted
    [InlineData("city", "city")]
    public void Trip_type_maps_to_dataset_usage(string purpose, string expected)
        => Assert.Equal(expected, Build(new Customer { RentalPurpose = purpose }).UsagePrimary);

    [Fact]
    public void Vietnamese_note_is_kept_and_mapped_to_car_description_tokens()
    {
        var p = Build(new Customer { PreferencesText = "Xe tiết kiệm xăng, an toàn, cốp rộng" });

        Assert.StartsWith("Xe tiết kiệm xăng, an toàn, cốp rộng | ", p.PreferencesText);
        Assert.Contains("fuel economy", p.PreferencesText);
        Assert.Contains("safety", p.PreferencesText);
        Assert.Contains("trunk", p.PreferencesText);
    }

    [Fact]
    public void Note_without_diacritics_is_recognised_too()
    {
        var p = Build(new Customer { PreferencesText = "can xe gam cao di duong deo" });
        Assert.Contains("suv", p.PreferencesText);
    }

    [Fact]
    public void Note_and_dropdowns_are_combined_instead_of_dropping_dropdowns()
    {
        var p = Build(new Customer
        {
            PreferencesText = "an toàn",
            PreferredVehicleType = "Limousine",
            PreferredFuelType = "Diesel",
            PreferredTransmission = "Automatic",
            PreferredSeats = 7
        });

        Assert.Contains("safety", p.PreferencesText);
        Assert.Contains("seats 7 seater", p.PreferencesText); // Limousine / 7 seats -> catalog 7-Seater
        Assert.Contains("gas", p.PreferencesText);            // Diesel -> dataset fuel "gas"
        Assert.DoesNotContain("automatic", p.PreferencesText, StringComparison.OrdinalIgnoreCase); // not in car descriptions
    }

    [Fact]
    public void Large_party_adds_seven_seat_tokens()
    {
        var p = Build(new Customer { FamilySize = 6 });
        Assert.Contains("seats 7 seater", p.PreferencesText);
    }

    [Fact]
    public void Empty_profile_uses_trip_type_text()
    {
        Assert.Equal("suv", Build(new Customer { RentalPurpose = "mountain_trip" }).PreferencesText);
        Assert.Equal("crossover safety", Build(new Customer()).PreferencesText);
    }

    [Fact]
    public void Unrelated_text_adds_nothing_and_does_not_false_match()
    {
        // "lịch sử" must not be read as "lịch sự"; "xăng" inside a phrase must not break word boundaries.
        Assert.Empty(PreferenceKeywords.FromNote("muốn xem lịch sử đơn hàng"));
        Assert.Equal("dong nguoi di choi", PreferenceKeywords.Normalize("Đông người, đi chơi!"));
    }
}
