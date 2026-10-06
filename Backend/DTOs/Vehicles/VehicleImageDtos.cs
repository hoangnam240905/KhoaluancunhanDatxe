namespace Backend.DTOs.Vehicles;

public record VehicleImageResponse(
    int VehicleImageId,
    int VehicleId,
    string ImageUrl,
    string? ImageType,
    bool IsPrimary,
    int DisplayOrder,
    DateTime CreatedAt);

public record CreateVehicleImageRequest(
    string ImageUrl,
    string? ImageType = null,
    bool IsPrimary = false,
    int DisplayOrder = 0);

public record UpdateVehicleImageRequest(
    string ImageUrl,
    string? ImageType = null,
    bool IsPrimary = false,
    int DisplayOrder = 0);
