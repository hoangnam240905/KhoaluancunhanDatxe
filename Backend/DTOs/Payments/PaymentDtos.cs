namespace Backend.DTOs.Payments;

public record CreatePaymentRequest(
    int BookingId,
    string? PaymentType,
    string? Method,
    string? TransactionRef = null,
    int? VehicleId = null);

public record PaymentResponse(
    int PaymentId,
    int BookingId,
    string? PaymentType,
    decimal Amount,
    string Method,
    string Status,
    string? TransactionRef,
    DateTime? PaidAt,
    DateTime CreatedAt);
