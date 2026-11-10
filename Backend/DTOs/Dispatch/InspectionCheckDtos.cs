namespace Backend.DTOs.Dispatch;

/// <summary>A driver's dispute of the dispatcher's pre-handover check (plan1.md Phần A).</summary>
public record InspectionDisputeResponse(
    int InspectionDisputeId,
    int BookingId,
    int InspectionId,
    string? RaisedByName,
    string Note,
    string Status,
    string? ResolvedByName,
    string? ResolutionNote,
    DateTime? ResolvedAt,
    DateTime CreatedAt);

public record RaiseDisputeRequest(string? Note);

public record ResolveDisputeRequest(string? Note);

/// <summary>Result: "Confirmed" (matches) or "Mismatch" (note required, opens an incident).</summary>
public record VerifyReturnRequest(string? Result, string? Note);
