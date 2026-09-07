namespace Backend.DTOs.Incidents;

public record CreateIncidentRequest(
    string? IncidentType,
    string? Description,
    DateTime? OccurredAt = null);

public record IncidentResponse(
    int IncidentId,
    int BookingId,
    int AssignmentId,
    int DriverId,
    string DriverName,
    string IncidentType,
    string Description,
    DateTime OccurredAt,
    string Status,
    DateTime CreatedAt);
