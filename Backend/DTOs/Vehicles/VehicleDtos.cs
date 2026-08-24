namespace Backend.DTOs.Vehicles;

public record VehicleTypeResponse(
    int TypeId,
    string TypeName,
    int SeatCapacity,
    decimal PricePerDay,
    decimal PricePerKm,
    string? Description,
    string? ImageUrl);

public record VehicleResponse(
    int VehicleId,
    int TypeId,
    string TypeName,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string? Color,
    string Status,
    int CurrentKm);

public record CreateVehicleRequest(
    int TypeId,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string? Color,
    int CurrentKm);

public record UpdateVehicleRequest(
    int TypeId,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string? Color,
    string Status,
    int CurrentKm);
