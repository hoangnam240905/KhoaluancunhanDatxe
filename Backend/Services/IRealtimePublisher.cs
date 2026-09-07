using Backend.DTOs.Realtime;

namespace Backend.Services;

public interface IRealtimePublisher
{
    Task PublishAsync(RealtimeEventDto evt, RealtimeAudience audience);
}

public sealed class NullRealtimePublisher : IRealtimePublisher
{
    public static readonly NullRealtimePublisher Instance = new();

    public Task PublishAsync(RealtimeEventDto evt, RealtimeAudience audience)
        => Task.CompletedTask;
}

public static class RealtimeNotify
{
    public static Task BookingStatusChanged(
        IRealtimePublisher? realtime,
        int customerId,
        int bookingId,
        string status,
        int? assignedVehicleId = null,
        int? driverId = null)
        => Publish(
            realtime,
            RealtimeEventTypes.BookingStatusChanged,
            RealtimeEntityTypes.Booking,
            bookingId,
            bookingId,
            RealtimeAudience.ForBooking(customerId, driverId),
            new Dictionary<string, object?>
            {
                ["status"] = status,
                ["assignedVehicleId"] = assignedVehicleId,
                ["driverId"] = driverId
            });

    public static Task AssignmentChanged(
        IRealtimePublisher? realtime,
        int customerId,
        int bookingId,
        int? assignmentId,
        int vehicleId,
        int? driverId,
        string bookingStatus)
        => Publish(
            realtime,
            RealtimeEventTypes.AssignmentChanged,
            RealtimeEntityTypes.TripAssignment,
            assignmentId ?? bookingId,
            bookingId,
            RealtimeAudience.ForBooking(customerId, driverId),
            new Dictionary<string, object?>
            {
                ["status"] = bookingStatus,
                ["vehicleId"] = vehicleId,
                ["driverId"] = driverId,
                ["assignmentId"] = assignmentId
            });

    public static Task TripStatusChanged(
        IRealtimePublisher? realtime,
        int customerId,
        int driverId,
        int bookingId,
        int assignmentId,
        string tripStatus,
        string bookingStatus)
        => Publish(
            realtime,
            RealtimeEventTypes.TripStatusChanged,
            RealtimeEntityTypes.TripAssignment,
            assignmentId,
            bookingId,
            RealtimeAudience.ForBooking(customerId, driverId),
            new Dictionary<string, object?>
            {
                ["tripStatus"] = tripStatus,
                ["bookingStatus"] = bookingStatus
            });

    public static Task VehicleStatusChanged(
        IRealtimePublisher? realtime,
        int vehicleId,
        string status,
        int? bookingId = null,
        int? customerId = null,
        int? driverId = null)
        => Publish(
            realtime,
            RealtimeEventTypes.VehicleStatusChanged,
            RealtimeEntityTypes.Vehicle,
            vehicleId,
            bookingId,
            customerId is int cid
                ? RealtimeAudience.ForBooking(cid, driverId)
                : RealtimeAudience.OperationsOnly(),
            new Dictionary<string, object?> { ["status"] = status });

    public static Task DriverStatusChanged(
        IRealtimePublisher? realtime,
        int driverId,
        string status)
        => Publish(
            realtime,
            RealtimeEventTypes.DriverStatusChanged,
            RealtimeEntityTypes.Driver,
            driverId,
            null,
            RealtimeAudience.ForDriver(driverId),
            new Dictionary<string, object?> { ["status"] = status });

    public static Task PaymentStatusChanged(
        IRealtimePublisher? realtime,
        int customerId,
        int bookingId,
        int paymentId,
        string status)
        => Publish(
            realtime,
            RealtimeEventTypes.PaymentStatusChanged,
            RealtimeEntityTypes.Payment,
            paymentId,
            bookingId,
            RealtimeAudience.ForBooking(customerId),
            new Dictionary<string, object?> { ["status"] = status });

    public static Task ContractStatusChanged(
        IRealtimePublisher? realtime,
        int customerId,
        int bookingId,
        int contractId,
        string status)
        => Publish(
            realtime,
            RealtimeEventTypes.ContractStatusChanged,
            RealtimeEntityTypes.Contract,
            contractId,
            bookingId,
            RealtimeAudience.ForBooking(customerId),
            new Dictionary<string, object?> { ["status"] = status });

    public static Task IncidentReported(
        IRealtimePublisher? realtime,
        int driverId,
        int bookingId,
        int incidentId,
        string incidentType)
        => Publish(
            realtime,
            RealtimeEventTypes.IncidentReported,
            RealtimeEntityTypes.Incident,
            incidentId,
            bookingId,
            RealtimeAudience.ForDriver(driverId),
            new Dictionary<string, object?> { ["incidentType"] = incidentType });

    private static Task Publish(
        IRealtimePublisher? realtime,
        string eventType,
        string entityType,
        int entityId,
        int? bookingId,
        RealtimeAudience audience,
        IReadOnlyDictionary<string, object?> payload)
    {
        if (realtime is null)
            return Task.CompletedTask;

        return realtime.PublishAsync(
            new RealtimeEventDto(eventType, entityType, entityId, bookingId, DateTime.UtcNow, payload),
            audience);
    }
}
