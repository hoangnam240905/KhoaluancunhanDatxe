namespace Backend.DTOs.Interactions;

public record CustomerVehicleInteractionResponse(
    int InteractionId,
    int CustomerId,
    int VehicleId,
    string InteractionType,
    DateTime CreatedAt);

public record CreateCustomerVehicleInteractionRequest(
    int VehicleId,
    string InteractionType);
