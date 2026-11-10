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
    DateTime CreatedAt,
    int? ResolvedByUserId = null,
    string? ResolvedByName = null,
    string? ResolvedByRole = null,
    DateTime? ResolvedAt = null,
    string? ResolutionNote = null);

/// <summary>Dispatcher closes an incident; the note says what was done.</summary>
public record ResolveIncidentRequest(string? Note);
