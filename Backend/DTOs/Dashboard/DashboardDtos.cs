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
    DashboardRecommendation Recommendation,
    int TotalCustomers,
    int ActiveFleet,
    IReadOnlyList<DashboardRevenuePoint> RevenueOverview,
    IReadOnlyList<DashboardRecentBooking> RecentBookings,
    IReadOnlyList<DashboardUpcomingPickup> UpcomingPickups,
    // Added later (defaults keep older clients/tests compiling):
    DashboardRevenueSummary? Revenue = null,
    DashboardHeatmap? RentalHeatmap = null,
    DashboardReviews? Reviews = null);

/// <summary>
/// Trip revenue for trips COMPLETED in the range (not deposits). Rental = quoted TotalAmount;
/// Adjustment = (FinalAmount ?? TotalAmount) − TotalAmount (extra km, late, fuel; can be negative).
/// </summary>
public record DashboardRevenueSummary(
    int CompletedTrips,
    decimal RentalAmount,
    decimal AdjustmentAmount,
    decimal TotalAmount,
    decimal DepositPaidAmount,
    int OutlierTrips,
    decimal? RentalChangePercent,
    string Note);

/// <summary>Pickup time heat: DayOfWeek 1 = Monday … 7 = Sunday, Hour 0–23 (Vietnam wall clock of StartDate).</summary>
public record DashboardHeatmap(
    IReadOnlyList<DashboardHeatCell> Cells,
    int Max,
    int Total,
    string Note);

public record DashboardHeatCell(int DayOfWeek, int Hour, int Count);

public record DashboardReviews(
    decimal? AverageRating,
    int Count,
    IReadOnlyList<DashboardLatestReview> Latest);

public record DashboardLatestReview(
    int ReviewId,
    int BookingId,
    int Rating,
    string? Comment,
    string VehicleLabel,
    string CustomerName,
    string DriverName,
    DateTime CreatedAt);

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
    string DepositPaidLabel,
    decimal? DepositPaidChangePercent,
    decimal? BookingsChangePercent);

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
    int CompletedCount,
    decimal RentalAmount = 0,
    decimal AdjustmentAmount = 0,
    decimal TotalAmount = 0,
    decimal? AverageRating = null,
    int ReviewCount = 0,
    int OutlierTrips = 0);

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

public record DashboardRevenuePoint(
    string Label,
    int Year,
    int Month,
    decimal Amount);

public record DashboardRecentBooking(
    int BookingId,
    string CustomerName,
    string VehicleLabel,
    DateTime StartDate,
    string Status,
    decimal TotalAmount);

public record DashboardUpcomingPickup(
    int BookingId,
    string CustomerName,
    string VehicleLabel,
    DateTime StartDate,
    string Status);
