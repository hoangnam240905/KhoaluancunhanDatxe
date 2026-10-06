using System.Net;
using System.Text;
using System.Text.Json;
using Backend.DTOs.Recommendations;
using Microsoft.AspNetCore.Http;
using Xunit;
using V7Rec = Backend.Services.Recommendation;

namespace Backend.Tests;

/// <summary>
/// Step 2: Backend C# → V7 Python RecommendationService integration.
/// Integration cases require RecommendationService running at http://127.0.0.1:8000.
/// </summary>
public class RecommendationPythonServiceTests
{
    private const string BaseUrl = "http://127.0.0.1:8000";

    private static PythonRecommendationRequest SampleCity(int topK = 5) => new(
        Age: 25,
        Kids: 0,
        FamilySize: 2,
        IncomeBracket: "medium",
        CityType: "urban",
        CommuteKmDay: 15,
        ChargingAccess: 0,
        AnnualKm: 10000,
        UsagePrimary: "city",
        BudgetMonthly: 500,
        PreferencesText: "reliable compact car for city commuting with good fuel economy",
        TopK: topK);

    private static PythonRecommendationRequest SampleFamily(int topK = 5) => new(
        Age: 42,
        Kids: 2,
        FamilySize: 5,
        IncomeBracket: "high",
        CityType: "suburban",
        CommuteKmDay: 35,
        ChargingAccess: 1,
        AnnualKm: 18000,
        UsagePrimary: "family",
        BudgetMonthly: 900,
        PreferencesText: "spacious family SUV with high safety score and large trunk",
        TopK: topK);

    private static async Task EnsurePythonHealthyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await http.GetAsync($"{BaseUrl}/health");
            Assert.True(
                response.IsSuccessStatusCode,
                $"Integration test requires RecommendationService at {BaseUrl}/health (got {(int)response.StatusCode}). Start: python -m uvicorn app:app --host 127.0.0.1 --port 8000");
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Assert.Fail(
                $"Integration test requires RecommendationService at {BaseUrl}. Start it first. Underlying: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static V7Rec.IRecommendationService CreateClient(string baseUrl, int timeoutSeconds = 30)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        return new V7Rec.RecommendationService(http);
    }

    [Fact]
    public async Task Integration_valid_profile_returns_top_k()
    {
        await EnsurePythonHealthyAsync();
        var client = CreateClient(BaseUrl);
        var request = SampleCity(topK: 5);

        var (data, error, status) = await client.GetRecommendationsAsync(request);

        Assert.Null(error);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(data);
        Assert.True(data!.Success);
        Assert.False(string.IsNullOrWhiteSpace(data.PredictedSegment));
        Assert.Equal(5, data.Recommendations.Count);
        foreach (var item in data.Recommendations)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CarId));
            Assert.False(string.IsNullOrWhiteSpace(item.Make));
            Assert.False(string.IsNullOrWhiteSpace(item.Model));
            Assert.False(string.IsNullOrWhiteSpace(item.Segment));
            Assert.True(item.FinalScore >= 0);
        }
    }

    [Fact]
    public async Task Integration_two_different_profiles_both_succeed()
    {
        await EnsurePythonHealthyAsync();
        var client = CreateClient(BaseUrl);

        var (a, errA, statusA) = await client.GetRecommendationsAsync(SampleCity());
        var (b, errB, statusB) = await client.GetRecommendationsAsync(SampleFamily());

        Assert.Null(errA);
        Assert.Null(errB);
        Assert.Equal(200, statusA);
        Assert.Equal(200, statusB);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.True(a!.Success && b!.Success);
        Assert.Equal(5, a.Recommendations.Count);
        Assert.Equal(5, b.Recommendations.Count);
        Assert.True(
            a.PredictedSegment != b.PredictedSegment
            || a.Recommendations[0].CarId != b.Recommendations[0].CarId);
    }

    [Fact]
    public async Task Unavailable_python_does_not_crash_returns_503()
    {
        var client = CreateClient("http://127.0.0.1:59999", timeoutSeconds: 2);

        var (data, error, status) = await client.GetRecommendationsAsync(SampleCity());

        Assert.Null(data);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal("Dịch vụ Recommendation V7 tạm thời không khả dụng.", error);
    }

    [Fact]
    public async Task Request_json_uses_snake_case_v7_fields()
    {
        string? capturedBody = null;
        var handler = new CaptureHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            var fake = """
                {
                  "success": true,
                  "predicted_segment": "Sedan",
                  "recommendations": [
                    {
                      "car_id": "car_0001",
                      "make": "Skoda",
                      "model": "Octavia",
                      "segment": "Sedan",
                      "final_score": 0.4
                    }
                  ],
                  "train_count": 1
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fake, Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://capture.test/")
        };
        var client = new V7Rec.RecommendationService(http);

        var (_, error, status) = await client.GetRecommendationsAsync(SampleCity(topK: 3));
        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.False(string.IsNullOrWhiteSpace(capturedBody));

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("age", out _));
        Assert.True(root.TryGetProperty("kids", out _));
        Assert.True(root.TryGetProperty("family_size", out _));
        Assert.True(root.TryGetProperty("income_bracket", out _));
        Assert.True(root.TryGetProperty("city_type", out _));
        Assert.True(root.TryGetProperty("commute_km_day", out _));
        Assert.True(root.TryGetProperty("charging_access", out _));
        Assert.True(root.TryGetProperty("annual_km", out _));
        Assert.True(root.TryGetProperty("usage_primary", out _));
        Assert.True(root.TryGetProperty("budget_monthly", out _));
        Assert.True(root.TryGetProperty("preferences_text", out _));
        Assert.True(root.TryGetProperty("top_k", out var topK));
        Assert.Equal(3, topK.GetInt32());
        Assert.False(root.TryGetProperty("familySize", out _));
        Assert.False(root.TryGetProperty("FamilySize", out _));
        Assert.False(root.TryGetProperty("topK", out _));
    }

    [Fact]
    public async Task Invalid_request_fails_locally_without_http()
    {
        var client = CreateClient("http://127.0.0.1:59999", timeoutSeconds: 1);
        var bad = SampleCity() with { Age = 5 };

        var (data, error, status) = await client.GetRecommendationsAsync(bad);

        Assert.Null(data);
        Assert.Equal(400, status);
        Assert.Equal("age phải từ 16 đến 100.", error);
    }

    private sealed class CaptureHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
