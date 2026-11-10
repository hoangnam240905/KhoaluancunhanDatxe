namespace Backend.DTOs.Realtime;

public static class RealtimeEventTypes
{
    public const string BookingStatusChanged = "BookingStatusChanged";
    public const string AssignmentChanged = "AssignmentChanged";
    public const string TripStatusChanged = "TripStatusChanged";
    public const string VehicleStatusChanged = "VehicleStatusChanged";
    public const string DriverStatusChanged = "DriverStatusChanged";
    public const string PaymentStatusChanged = "PaymentStatusChanged";
    public const string ContractStatusChanged = "ContractStatusChanged";
    public const string IncidentReported = "IncidentReported";
    // Two-sided vehicle check on with-driver trips (plan1.md Phần A).
    public const string PreHandoverRecorded = "PreHandoverRecorded";
    public const string InspectionDisputeRaised = "InspectionDisputeRaised";
    public const string InspectionDisputeResolved = "InspectionDisputeResolved";
    public const string DispatcherReminded = "DispatcherReminded";
}

public static class RealtimeEntityTypes
{
    public const string Booking = "Booking";
    public const string TripAssignment = "TripAssignment";
    public const string Vehicle = "Vehicle";
    public const string Driver = "Driver";
    public const string Payment = "Payment";
    public const string Contract = "Contract";
    public const string Incident = "Incident";
    public const string Inspection = "Inspection";
    public const string InspectionDispute = "InspectionDispute";
}

public static class RealtimeGroups
{
    public const string Dispatcher = "dispatcher";
    public const string Admin = "admin";

    public static string Customer(int customerId) => $"customer:{customerId}";
    public static string Driver(int driverId) => $"driver:{driverId}";
}

public record RealtimeEventDto(
    string EventType,
    string EntityType,
    int EntityId,
    int? BookingId,
    DateTime Timestamp,
    IReadOnlyDictionary<string, object?>? Payload);

public readonly record struct RealtimeAudience(int? CustomerId, int? DriverId, bool Operations)
{
    public static RealtimeAudience ForBooking(int customerId, int? driverId = null)
        => new(customerId, driverId, true);

    public static RealtimeAudience ForDriver(int driverId)
        => new(null, driverId, true);

    public static RealtimeAudience OperationsOnly()
        => new(null, null, true);
}
