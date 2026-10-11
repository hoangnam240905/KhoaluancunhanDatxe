namespace Backend.Entities;

public class RecommendationLog
{
    public int RecommendationId { get; set; }
    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public double Score { get; set; }
    public string? RecommendationReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
