namespace Backend.Constants;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Dispatcher = "Dispatcher";
    public const string Customer = "Customer";
    public const string Driver = "Driver";
}

public static class BookingStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class DriverStatuses
{
    public const string Available = "Available";
    public const string Busy = "Busy";
    public const string Offline = "Offline";
}

public static class VehicleStatuses
{
    public const string Available = "Available";
    public const string Rented = "Rented";
    public const string Maintenance = "Maintenance";
    public const string Inactive = "Inactive";
}

public static class TripAssignmentStatuses
{
    public const string Assigned = "Assigned";
    public const string Accepted = "Accepted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
