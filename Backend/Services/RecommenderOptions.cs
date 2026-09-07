namespace Backend.Services;

public class RecommenderOptions
{
    public const string SectionName = "Recommender";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
    public int TimeoutSeconds { get; set; } = 8;
}
