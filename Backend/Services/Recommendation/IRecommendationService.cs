using Backend.DTOs.Recommendations;

namespace Backend.Services.Recommendation;

/// <summary>
/// HTTP client for the standalone V7 Python RecommendationService.
/// Does not implement ranking or ML — integration only.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Calls Python POST /recommend.
    /// On success: Data is set, Error is null, StatusCode 200.
    /// On validation / Python / network failure: Data is null, Error describes the issue, StatusCode is set.
    /// Does not throw for unreachable Python service.
    /// </summary>
    Task<(PythonRecommendationResponse? Data, string? Error, int StatusCode)> GetRecommendationsAsync(
        PythonRecommendationRequest request,
        CancellationToken cancellationToken = default);
}
