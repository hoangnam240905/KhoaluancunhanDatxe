namespace Backend.Entities;

public class VehicleImage
{
    public int VehicleImageId { get; set; }
    public int VehicleId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ImageType { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
