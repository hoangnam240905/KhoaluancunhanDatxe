namespace Backend.DTOs.Vehicles;

public record VehicleTypeResponse(
    int TypeId,
    string TypeName,
    int SeatCapacity,
    decimal PricePerDay,
    decimal PricePerKm,
    string? Description,
    string? ImageUrl);

public record AdminVehicleTypeResponse(
    int TypeId,
    string TypeName,
    int SeatCapacity,
    decimal PricePerDay,
    decimal PricePerKm,
    decimal DriverFeePerDay,
    decimal SelfDrivePricePerDay,
    decimal SelfDriveIncludedKmPerDay,
    decimal SelfDriveExtraKmPrice,
    decimal WithDriverDepositAmount,
    decimal SelfDriveDepositAmount,
    string? Description,
    string? ImageUrl,
    bool IsActive);

public record CreateVehicleTypeRequest(
    string? TypeName,
    decimal PricePerDay,
    decimal PricePerKm);

public record UpdateVehicleTypePricingRequest(
    decimal? PricePerDay,
    decimal? PricePerKm,
    decimal? DriverFeePerDay,
    decimal? SelfDrivePricePerDay,
    decimal? SelfDriveIncludedKmPerDay,
    decimal? SelfDriveExtraKmPrice,
    decimal? WithDriverDepositAmount,
    decimal? SelfDriveDepositAmount);

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
    int CurrentKm,
    string? RegistrationNumber = null,
    DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null,
    DateOnly? InsuranceExpiryDate = null);

public record CreateVehicleRequest(
    int TypeId,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string? Color,
    int CurrentKm,
    string? RegistrationNumber = null,
    DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null,
    DateOnly? InsuranceExpiryDate = null);

public record UpdateVehicleRequest(
    int TypeId,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    string? Color,
    string Status,
    int CurrentKm,
    string? RegistrationNumber = null,
    DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null,
    DateOnly? InsuranceExpiryDate = null);
