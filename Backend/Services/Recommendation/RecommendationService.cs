using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.DTOs.Recommendations;

namespace Backend.Services.Recommendation;

/// <summary>
/// Backend → V7 Python RecommendationService client.
/// Model training stays in Python; this class only POSTs /recommend.
/// </summary>
public class RecommendationService(HttpClient http) : IRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
        PythonRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (validation is not null)
            return (null, validation, StatusCodes.Status400BadRequest);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("recommend", request, JsonOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout
            return Unavailable();
        }
        catch (TaskCanceledException)
        {
            return Unavailable();
        }
        catch (HttpRequestException)
        {
            return Unavailable();
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            var detail = await ReadErrorAsync(response, cancellationToken);
            return (null, detail ?? "Yêu cầu gợi ý không hợp lệ.", StatusCodes.Status400BadRequest);
        }

        if (!response.IsSuccessStatusCode)
            return Unavailable();

        PythonRecommendationResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<PythonRecommendationResponse>(
                JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return (null, "Phản hồi gợi ý không hợp lệ.", StatusCodes.Status502BadGateway);
        }

        if (payload is null)
            return (null, "Phản hồi gợi ý trống.", StatusCodes.Status502BadGateway);

        if (!payload.Success)
            return (null, "Dịch vụ gợi ý trả success=false.", StatusCodes.Status502BadGateway);

        if (string.IsNullOrWhiteSpace(payload.PredictedSegment))
            return (null, "Phản hồi thiếu predicted_segment.", StatusCodes.Status502BadGateway);

        if (payload.Recommendations is null || payload.Recommendations.Count == 0)
            return (null, "Phản hồi không có recommendations.", StatusCodes.Status502BadGateway);

        return (payload, null, StatusCodes.Status200OK);
    }

    private static string? Validate(PythonRecommendationRequest request)
    {
        if (request.Age is < 16 or > 100)
            return "age phải từ 16 đến 100.";
        if (request.Kids < 0)
            return "kids không được âm.";
        if (request.FamilySize < 1)
            return "family_size phải >= 1.";
        if (string.IsNullOrWhiteSpace(request.IncomeBracket))
            return "income_bracket bắt buộc.";
        if (string.IsNullOrWhiteSpace(request.CityType))
            return "city_type bắt buộc.";
        if (request.CommuteKmDay < 0)
            return "commute_km_day không được âm.";
        if (request.ChargingAccess is < 0 or > 1)
            return "charging_access phải là 0 hoặc 1.";
        if (request.AnnualKm < 0)
            return "annual_km không được âm.";
        if (string.IsNullOrWhiteSpace(request.UsagePrimary))
            return "usage_primary bắt buộc.";
        if (request.BudgetMonthly < 0)
            return "budget_monthly không được âm.";
        if (string.IsNullOrWhiteSpace(request.PreferencesText))
            return "preferences_text bắt buộc.";
        if (request.TopK is < 1 or > 50)
            return "top_k phải từ 1 đến 50.";
        return null;
    }

    private static async Task<string?> ReadErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            if (json.ValueKind == JsonValueKind.Object)
            {
                if (json.TryGetProperty("detail", out var detail))
                {
                    if (detail.ValueKind == JsonValueKind.String)
                        return detail.GetString();
                    return detail.ToString();
                }

                if (json.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }
        }
        catch
        {
            /* ignore parse errors */
        }

        return null;
    }

    private static (PythonRecommendationResponse? Data, string? Error, int StatusCode) Unavailable()
        => (null, "Dịch vụ Recommendation V7 tạm thời không khả dụng.", StatusCodes.Status503ServiceUnavailable);
}
