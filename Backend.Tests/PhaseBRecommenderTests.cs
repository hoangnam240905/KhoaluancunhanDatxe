using System.Net;
using System.Text;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PhaseBRecommenderTests
{
    [Fact]
    public async Task FastApi_client_maps_success_payload()
    {
        var json = """
            {"items":[{"vehicleTypeId":1,"typeName":"Sedan","score":2.15,"ranking":1,"avgRating":4.0,"pricePerDay":800000,"availableCount":2}]}
            """;
        var http = new HttpClient(new StaticHandler(HttpStatusCode.OK, json))
        {
            BaseAddress = new Uri("http://recommender.test/")
        };
        var (items, error, status) = await new FastApiRecommenderClient(http).RecommendAsync(EmptySnapshot());
        Assert.Null(error);
        Assert.Equal(200, status);
        var row = Assert.Single(items!);
        Assert.Equal(1, row.VehicleTypeId);
        Assert.Equal(2.15m, row.Score);
        Assert.Equal(2, row.AvailableCount);
    }

    [Fact]
    public async Task FastApi_client_unavailable_does_not_throw()
    {
        var http = new HttpClient(new StaticHandler(HttpStatusCode.ServiceUnavailable, """{"message":"down"}"""))
        {
            BaseAddress = new Uri("http://recommender.test/")
        };
        var (items, error, status) = await new FastApiRecommenderClient(http).RecommendAsync(EmptySnapshot());
        Assert.Null(items);
        Assert.Equal(503, status);
        Assert.Equal("Dịch vụ gợi ý xe tạm thời không khả dụng.", error);
    }

    [Fact]
    public async Task FastApi_client_connection_failure_returns_503()
    {
        var http = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://recommender.test/")
        };
        var (items, error, status) = await new FastApiRecommenderClient(http).RecommendAsync(EmptySnapshot());
        Assert.Null(items);
        Assert.Equal(503, status);
        Assert.Equal("Dịch vụ gợi ý xe tạm thời không khả dụng.", error);
    }

    [Fact]
    public async Task Orchestrator_returns_503_when_recommender_down()
    {
        using var iso = new IsolatedCarRentalDb();
        var svc = new RecommendationService(
            iso.Db, new ScheduleConflictService(iso.Db), new UnavailableRecommenderClient());
        var start = new DateTime(2026, 11, 1, 8, 0, 0);
        var end = new DateTime(2026, 11, 3, 18, 0, 0);
        var (data, error, status) = await svc.RecommendAsync(start, end, null, null, null);
        Assert.Null(data);
        Assert.Equal(503, status);
        Assert.Equal("Dịch vụ gợi ý xe tạm thời không khả dụng.", error);
    }

    [Fact]
    public async Task Api_recommended_still_anonymous_ok_with_test_double()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var rec = await client.GetAsync("/api/vehicle-types/recommended?startDate=2026-11-01T08:00:00&endDate=2026-11-03T18:00:00");
        Assert.Equal(HttpStatusCode.OK, rec.StatusCode);
    }

    private static RecommenderSnapshotRequest EmptySnapshot() => new(
        new DateTime(2026, 11, 1, 8, 0, 0),
        new DateTime(2026, 11, 3, 18, 0, 0),
        null, null, null, [], [], []);

    private sealed class StaticHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }
}
