namespace Backend.DTOs.Contracts;

public record ContractResponse(
    int ContractId,
    int BookingId,
    string ContractNumber,
    string Status,
    int CustomerId,
    string CustomerName,
    string VehicleTypeName,
    string RentalMode,
    string PickupAddress,
    string DropoffAddress,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalAmount,
    decimal? DepositAmount,
    DateTime CreatedAt,
    DateTime? SignedAt);
