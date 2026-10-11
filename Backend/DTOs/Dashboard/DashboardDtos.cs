using Backend.DTOs.Maintenance;

namespace Backend.DTOs.Dashboard;

public record DashboardResponse(
    DashboardRange Range,
    DashboardSummary Summary,
    DashboardBookings Bookings,
    DashboardPayments Payments,
    DashboardVehicles Vehicles,
    DashboardDrivers Drivers,
    IReadOnlyList<DashboardTopVehicle> TopVehicles,
    DashboardMaintenance Maintenance,
    DashboardRecommendation Recommendation);

public record DashboardRange(
    DateTime From,
    DateTime To,
    bool DefaultRangeApplied,
    string TimeRangeNote,
    string SnapshotNote);

public record DashboardSummary(
    int TotalBookings,
    int CompletedBookings,
    int CancelledBookings,
    decimal DepositPaidAmount,
    string DepositPaidLabel);

public record DashboardBookings(
    int Total,
    int Pending,
    int Confirmed,
    int Assigned,
    int InProgress,
    int Completed,
    int Cancelled,
    DashboardDriverTrips DriverTrips);

public record DashboardDriverTrips(
    int Assigned,
    int Accepted,
    int InProgress,
    int Completed,
    int Cancelled);

public record DashboardPayments(
    int PaidDepositCount,
    int PendingDepositCount,
    int FailedDepositCount,
    decimal DepositPaidAmount,
    string AmountLabel,
    string MetricNote);

public record DashboardVehicles(
    int Total,
    int Available,
    int Rented,
    int Maintenance,
    int Inactive,
    string SnapshotNote);

public record DashboardDrivers(
    int Total,
    int Available,
    int Busy,
    int Offline,
    string SnapshotNote,
    IReadOnlyList<DashboardDriverRow> Performance);

public record DashboardDriverRow(
    int DriverId,
    string FullName,
    int CompletedAssignments,
    int CancelledAssignments,
    int IncidentCount,
    decimal? AverageReviewRating,
    decimal? AssignmentCompletionRate);

public record DashboardTopVehicle(
    int VehicleId,
    string LicensePlate,
    string Brand,
    string Model,
    int CompletedCount);

public record DashboardMaintenance(
    int AlertCount,
    IReadOnlyList<MaintenanceAlertResponse> Alerts);

public record DashboardRecommendation(
    int TotalBookings,
    int RecommendedBookings,
    decimal? AttributionShare,
    int RecommendedCompletedBookings,
    decimal? AverageRatingRecommendedReviewed,
    string MetricNote);
