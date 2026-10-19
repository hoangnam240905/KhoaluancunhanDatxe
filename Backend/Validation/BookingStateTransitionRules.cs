using Backend.Constants;

namespace Backend.Validation;

/// <summary>
/// Canonical booking lifecycle. Specialized flows (confirm/assign/start/complete)
/// remain the only way to move forward; PATCH may only cancel before the trip starts
/// (Assigned is cancellable; the start-time and customer window are enforced in BookingService).
/// Pending → Assigned is kept because AssignTripAsync already allows it.
/// </summary>
public static class BookingStateTransitionRules
{
    public const string InvalidTransition = "Không thể chuyển trạng thái đơn này.";

    public static bool CanTransition(string? fromStatus, string? toStatus)
    {
        if (string.IsNullOrWhiteSpace(fromStatus) || string.IsNullOrWhiteSpace(toStatus))
            return false;
        if (fromStatus == toStatus)
            return false;

        return (fromStatus, toStatus) switch
        {
            (BookingStatuses.Pending, BookingStatuses.Confirmed) => true,
            (BookingStatuses.Pending, BookingStatuses.Assigned) => true,
            (BookingStatuses.Pending, BookingStatuses.Cancelled) => true,
            (BookingStatuses.Confirmed, BookingStatuses.Assigned) => true,
            (BookingStatuses.Confirmed, BookingStatuses.Cancelled) => true,
            (BookingStatuses.Assigned, BookingStatuses.InProgress) => true,
            (BookingStatuses.Assigned, BookingStatuses.Cancelled) => true,
            (BookingStatuses.InProgress, BookingStatuses.Completed) => true,
            _ => false
        };
    }

    public static bool CanPatch(string? fromStatus, string? toStatus)
        => toStatus == BookingStatuses.Cancelled
           && fromStatus is BookingStatuses.Pending or BookingStatuses.Confirmed or BookingStatuses.Assigned
           && CanTransition(fromStatus, toStatus);

    public static bool CanConfirm(string? fromStatus)
        => CanTransition(fromStatus, BookingStatuses.Confirmed);
}
