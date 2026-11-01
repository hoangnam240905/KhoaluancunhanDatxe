using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.DTOs.Vehicles;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class FastApiRecommenderClient(HttpClient http) : IRecommenderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<(List<VehicleTypeRecommendationResponse>? Items, string? Error, int StatusCode)> RecommendAsync(
        RecommenderSnapshotRequest snapshot,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("recommend", snapshot, JsonOptions, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return Unavailable();
        }
        catch (HttpRequestException)
        {
            return Unavailable();
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
            return (null, await ReadMessageAsync(response) ?? "Dữ liệu gợi ý không hợp lệ.", StatusCodes.Status400BadRequest);

        if (!response.IsSuccessStatusCode)
            return Unavailable();

        var payload = await response.Content.ReadFromJsonAsync<RecommenderPythonResponse>(JsonOptions, cancellationToken);
        var items = (payload?.Items ?? [])
            .Select(i => new VehicleTypeRecommendationResponse(
                i.VehicleTypeId,
                i.TypeName,
                i.Score,
                i.AvgRating,
                i.PricePerDay,
                i.AvailableCount))
            .ToList();
        return (items, null, StatusCodes.Status200OK);
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            if (json.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString();
            if (json.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
        }
        catch
        {
            /* ignore parse errors */
        }

        return null;
    }

    private static (List<VehicleTypeRecommendationResponse>? Items, string? Error, int StatusCode) Unavailable()
        => (null, "Dịch vụ gợi ý xe tạm thời không khả dụng.", StatusCodes.Status503ServiceUnavailable);
}
