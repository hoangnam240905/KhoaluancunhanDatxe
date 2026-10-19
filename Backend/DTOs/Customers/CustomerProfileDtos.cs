namespace Backend.DTOs.Customers;

/// <summary>Customer self-service profile for V7 recommendation (own record only).</summary>
public record CustomerProfileResponse(
    int CustomerId,
    string Email,
    string FullName,
    string? Phone,
    string? Address,
    DateOnly? DateOfBirth,
    int? NumberOfChildren,
    int? FamilySize,
    string? IncomeRange,
    string? CityType,
    double? CommuteKmPerDay,
    int? AnnualMileage,
    string? RentalPurpose,
    double? BudgetMin,
    double? BudgetMax,
    string? PreferredVehicleType,
    string? PreferredFuelType,
    string? PreferredTransmission,
    int? PreferredSeats,
    string? PreferencesText,
    double? TripBudgetPerDay = null);

/// <summary>Update V7-related Customer fields. Null clears optional fields (full replace of these columns).</summary>
public record UpdateCustomerProfileRequest(
    DateOnly? DateOfBirth,
    int? NumberOfChildren,
    int? FamilySize,
    string? IncomeRange,
    string? CityType,
    double? CommuteKmPerDay,
    int? AnnualMileage,
    string? RentalPurpose,
    double? BudgetMin,
    double? BudgetMax,
    string? PreferredVehicleType = null,
    string? PreferredFuelType = null,
    string? PreferredTransmission = null,
    int? PreferredSeats = null,
    string? PreferencesText = null,
    string? Address = null,
    double? TripBudgetPerDay = null);
