using Backend.DTOs.Recommendations;
using Backend.Options;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md (form hồ sơ du lịch) — bước 4: Backend asks V7 for the travel segment rules.</summary>
public class PhaseTravelProfileStep4Tests
{
    private const int Customer = 3;

    [Fact]
    public async Task Travel_segment_rules_are_sent_by_default()
    {
        using var iso = new IsolatedCarRentalDb();
        var python = new CapturingPython();

        await Mapping(iso, new RecommendationServiceOptions(), python)
            .GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.Equal("travel", python.LastRequest!.SegmentRules);
    }

    [Theory]
    [InlineData("v7", "v7")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task Segment_rules_follow_configuration(string? configured, string? sent)
    {
        using var iso = new IsolatedCarRentalDb();
        var python = new CapturingPython();

        await Mapping(iso, new RecommendationServiceOptions { SegmentRules = configured }, python)
            .GetRecommendationsForCustomerAsync(Customer, topK: 5);

        Assert.Equal(sent, python.LastRequest!.SegmentRules);
    }

    [Fact]
    public void Omitted_segment_rules_are_not_serialized()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var request = new PythonRecommendationRequest(34, 0, 2, "mid", "urban", 22, 0, 14237.5, "family", 2276, "x");

        Assert.DoesNotContain("segment_rules", System.Text.Json.JsonSerializer.Serialize(request, options));
        Assert.Contains("\"segment_rules\":\"travel\"",
            System.Text.Json.JsonSerializer.Serialize(request with { SegmentRules = "travel" }, options));
    }

    // ---------- helpers ----------

    private static RecommendationMappingService Mapping(
        IsolatedCarRentalDb iso, RecommendationServiceOptions options, IRecommendationService python)
        => new(iso.Db, python,
            Microsoft.Extensions.Options.Options.Create(options),
            new V7CarIdVehicleMapStore(),
            NullLogger<RecommendationMappingService>.Instance);

    private sealed class CapturingPython : IRecommendationService
    {
        public PythonRecommendationRequest? LastRequest { get; private set; }

        public Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
            PythonRecommendationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult<(PythonRecommendationResponse?, string?, int)>(
                (new PythonRecommendationResponse(true, "Sedan", [], 1), null, StatusCodes.Status200OK));
        }
    }
}
