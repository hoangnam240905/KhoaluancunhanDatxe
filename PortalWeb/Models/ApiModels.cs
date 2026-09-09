using System.Text.Json.Serialization;

namespace PortalWeb.Models;

public record LoginRequest(string Email, string Password);
public record RegisterCustomerRequest(string Email, string Password, string FullName, string? Phone, string? Address, string? IdNumber, DateOnly? DateOfBirth, string? ConfirmPassword = null);
public record AuthResponse(string Token, int UserId, string Email, string FullName, string Role, DateTime ExpiresAt);
public record RegisterPendingResponse(string Email, string Message, bool RequiresVerification);
public record VerifyEmailRequest(string Email, string Otp);
public record ResendVerificationRequest(string Email);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Otp, string NewPassword, string? ConfirmPassword);
public record GoogleLoginRequest(string IdToken);
public record LoginOptionsResponse(bool GoogleEnabled, string? GoogleClientId);
public record MessageResponse(string Message);
public record ApiError(string Message);

public record VehicleTypeResponse(int TypeId, string TypeName, int SeatCapacity, decimal PricePerDay, decimal PricePerKm, string? Description, string? ImageUrl);
public record AdminVehicleTypeResponse(
    int TypeId, string TypeName, int SeatCapacity,
    decimal PricePerDay, decimal PricePerKm, decimal DriverFeePerDay,
    decimal SelfDrivePricePerDay, decimal SelfDriveIncludedKmPerDay, decimal SelfDriveExtraKmPrice,
    decimal WithDriverDepositAmount, decimal SelfDriveDepositAmount,
    string? Description, string? ImageUrl, bool IsActive);
public record UpdateVehicleTypePricingRequest(
    decimal PricePerDay, decimal PricePerKm, decimal DriverFeePerDay,
    decimal SelfDrivePricePerDay, decimal SelfDriveIncludedKmPerDay, decimal SelfDriveExtraKmPrice,
    decimal WithDriverDepositAmount, decimal SelfDriveDepositAmount);
public record VehicleTypeRecommendationResponse(
    int VehicleTypeId, string TypeName, decimal Score, decimal AvgRating, decimal PricePerDay, int AvailableCount);
public record VehicleResponse(
    int VehicleId, int TypeId, string TypeName, string LicensePlate, string Brand, string Model, int Year,
    string? Color, string Status, int CurrentKm,
    string? RegistrationNumber = null, DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null, DateOnly? InsuranceExpiryDate = null);
public record MaintenanceRecordResponse(
    int MaintenanceId, int VehicleId, string MaintenanceType, DateTime ScheduledDate, DateTime? CompletedDate,
    int? OdometerAtMaintenance, decimal? Cost, string? Notes, DateTime CreatedAt);
public record CreateMaintenanceRequest(
    string MaintenanceType, DateTime ScheduledDate, DateTime? CompletedDate, int? OdometerAtMaintenance, decimal? Cost, string? Notes);
public record CompleteMaintenanceRequest(int? OdometerAtMaintenance);
public record MaintenanceAlertResponse(
    int VehicleId, string LicensePlate, int? KmSinceLastMaintenance, int DaysSinceLastMaintenance, string Reason);
public record VehicleOperationalProfileResponse(
    VehicleResponse Vehicle,
    int SeatCapacity,
    int CurrentKm,
    decimal? LatestFuelLevel,
    DateTime? LastOperationalUpdateAt,
    string? LatestExteriorCondition,
    string? LatestTechnicalCondition,
    string? LatestNotes,
    VehicleInspectionResponse? LatestHandover,
    VehicleInspectionResponse? LatestReturn,
    VehicleInspectionResponse? LatestInspection,
    decimal? ActualKm,
    MaintenanceRecordResponse? LatestMaintenance,
    MaintenanceRecordResponse? OpenMaintenance,
    bool HasOpenMaintenance,
    string MaintenanceStatus,
    string MaintenanceStatusLabel,
    string? MaintenanceAlertReason,
    IReadOnlyList<VehicleInspectionResponse>? RecentInspections);
public record DashboardRange(DateTime From, DateTime To, bool DefaultRangeApplied, string TimeRangeNote, string SnapshotNote);
public record DashboardSummary(int TotalBookings, int CompletedBookings, int CancelledBookings, decimal DepositPaidAmount, string DepositPaidLabel);
public record DashboardDriverTrips(int Assigned, int Accepted, int InProgress, int Completed, int Cancelled);
public record DashboardBookings(int Total, int Pending, int Confirmed, int Assigned, int InProgress, int Completed, int Cancelled, DashboardDriverTrips DriverTrips);
public record DashboardPayments(int PaidDepositCount, int PendingDepositCount, int FailedDepositCount, decimal DepositPaidAmount, string AmountLabel, string MetricNote);
public record DashboardVehicles(int Total, int Available, int Rented, int Maintenance, int Inactive, string SnapshotNote);
public record DashboardDriverRow(int DriverId, string FullName, int CompletedAssignments, int CancelledAssignments, int IncidentCount, decimal? AverageReviewRating, decimal? AssignmentCompletionRate);
public record DashboardDrivers(int Total, int Available, int Busy, int Offline, string SnapshotNote, IReadOnlyList<DashboardDriverRow> Performance);
public record DashboardTopVehicle(int VehicleId, string LicensePlate, string Brand, string Model, int CompletedCount);
public record DashboardMaintenance(int AlertCount, IReadOnlyList<MaintenanceAlertResponse> Alerts);
public record DashboardRecommendation(int TotalBookings, int RecommendedBookings, decimal? AttributionShare, int RecommendedCompletedBookings, decimal? AverageRatingRecommendedReviewed, string MetricNote);
public record DashboardResponse(
    DashboardRange Range, DashboardSummary Summary, DashboardBookings Bookings, DashboardPayments Payments,
    DashboardVehicles Vehicles, DashboardDrivers Drivers, IReadOnlyList<DashboardTopVehicle> TopVehicles,
    DashboardMaintenance Maintenance, DashboardRecommendation Recommendation);
public record CreateVehicleRequest(
    int TypeId, string LicensePlate, string Brand, string Model, int Year, string? Color, int CurrentKm,
    string? RegistrationNumber = null, DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null, DateOnly? InsuranceExpiryDate = null);
public record UpdateVehicleRequest(
    int TypeId, string LicensePlate, string Brand, string Model, int Year, string? Color, string Status, int CurrentKm,
    string? RegistrationNumber = null, DateOnly? RegistrationExpiryDate = null,
    DateOnly? InspectionExpiryDate = null, DateOnly? InsuranceExpiryDate = null);

public record CreateBookingRequest(int VehicleTypeId, string PickupAddress, string DropoffAddress, decimal? PickupLat, decimal? PickupLng, decimal? DropoffLat, decimal? DropoffLng, DateTime StartDate, DateTime EndDate, decimal? EstimatedDistance, string? Notes, string? RentalMode = null, int? VehicleId = null);
public record AssignedVehicleResponse(int VehicleId, string LicensePlate, string Brand, string Model, string Status);
public record BookingFeeResponse(int FeeId, string FeeType, string? Description, decimal Amount, DateTime CreatedAt);
public record VehicleInspectionResponse(int InspectionId, int BookingId, int VehicleId, string InspectionType, DateTime ActualAt, decimal? OdometerKm, decimal? FuelLevel, string? Condition, string? Notes, DateTime CreatedAt, string? ExteriorCondition = null, string? TechnicalCondition = null);
public record BookingQuoteResponse(
    int VehicleTypeId,
    string VehicleTypeName,
    string RentalMode,
    DateTime StartDate,
    DateTime EndDate,
    decimal QuotedPricePerDay,
    decimal QuotedPricePerKm,
    int QuotedDays,
    decimal EstimatedDistance,
    decimal RentalAmount,
    decimal DistanceAmount,
    decimal TotalAmount,
    decimal DriverAmount,
    decimal IncludedKm,
    decimal ExtraKm,
    decimal ExtraKmPrice,
    decimal DepositAmount);
public record PaymentResponse(int PaymentId, int BookingId, string? PaymentType, decimal Amount, string Method, string Status, string? TransactionRef, DateTime? PaidAt, DateTime CreatedAt);
public record CreatePaymentRequest(int BookingId, string? PaymentType, string? Method, string? TransactionRef = null, int? VehicleId = null);

public record ContractResponse(
    int ContractId,
    int BookingId,
    string ContractNumber,
    string Status,
    int CustomerId,
    string CustomerName,
    string VehicleTypeName,
    string RentalMode,
    string PickupAddress,
    string DropoffAddress,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalAmount,
    decimal? DepositAmount,
    DateTime CreatedAt,
    DateTime? SignedAt);
public record BookingResponse(
    int BookingId,
    int CustomerId,
    string CustomerName,
    int VehicleTypeId,
    string VehicleTypeName,
    string PickupAddress,
    string DropoffAddress,
    DateTime StartDate,
    DateTime EndDate,
    decimal? EstimatedDistance,
    decimal TotalAmount,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    TripAssignmentResponse? Assignment,
    string RentalMode = "WithDriver",
    AssignedVehicleResponse? AssignedVehicle = null,
    decimal? QuotedPricePerDay = null,
    decimal? QuotedPricePerKm = null,
    int? QuotedDays = null,
    decimal? QuotedDriverFeePerDay = null,
    decimal? QuotedSelfDriveIncludedKmPerDay = null,
    decimal? QuotedSelfDriveExtraKmPrice = null,
    decimal? QuotedDepositAmount = null,
    decimal? FinalAmount = null,
    IReadOnlyList<BookingFeeResponse>? Fees = null,
    decimal? FinalBaseAmount = null,
    decimal? TotalFees = null,
    IReadOnlyList<VehicleInspectionResponse>? Inspections = null);
public record TripAssignmentResponse(int AssignmentId, int DriverId, string DriverName, string? DriverPhone, int VehicleId, string LicensePlate, string Status, DateTime AssignedAt);
public record UpdateBookingStatusRequest(string Status, string? Note);
public record AssignTripRequest(int? DriverId, int VehicleId);

public class VehicleConditionRequest
{
    public decimal? OdometerKm { get; set; }
    public decimal? FuelLevel { get; set; }
    public string? Condition { get; set; }
    public string? ExteriorCondition { get; set; }
    public string? TechnicalCondition { get; set; }
    public string? Notes { get; set; }

    [JsonIgnore]
    public bool HasValues =>
        OdometerKm is not null ||
        FuelLevel is not null ||
        !string.IsNullOrWhiteSpace(Condition) ||
        !string.IsNullOrWhiteSpace(ExteriorCondition) ||
        !string.IsNullOrWhiteSpace(TechnicalCondition) ||
        !string.IsNullOrWhiteSpace(Notes);

    public VehicleConditionRequest ForApi() => new()
    {
        OdometerKm = OdometerKm,
        FuelLevel = FuelLevel,
        Condition = string.IsNullOrWhiteSpace(Condition) ? null : Condition.Trim(),
        ExteriorCondition = string.IsNullOrWhiteSpace(ExteriorCondition) ? null : ExteriorCondition.Trim(),
        TechnicalCondition = string.IsNullOrWhiteSpace(TechnicalCondition) ? null : TechnicalCondition.Trim(),
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
    };
}
public record VehicleAlternativeResponse(int VehicleId, string LicensePlate, string Brand, string Model, string Status);
public record DriverAlternativeResponse(int DriverId, string FullName, string? Phone, string Status);
public record AssignConflictResponse(
    string Message,
    string ConflictType,
    IReadOnlyList<VehicleAlternativeResponse> VehicleAlternatives,
    IReadOnlyList<DriverAlternativeResponse> DriverAlternatives);
public record CreateReviewRequest(byte Rating, string? Comment);
public record DriverResponse(int DriverId, string FullName, string Email, string? Phone, string LicenseNumber, DateOnly LicenseExpiry, string Status, decimal AverageRating, int TotalTrips);
public record DispatchVehicleStatusResponse(
    int VehicleId, string LicensePlate, string TypeName, string VehicleStatus,
    string? DriverName, string? DriverStatus, int? BookingId, int? AssignmentId,
    DateTime? StartDate, DateTime? EndDate, string? RentalMode);
public record DispatchDriverStatusResponse(
    int DriverId, string FullName, string DriverStatus, bool IsActive,
    string? LicensePlate, int? BookingId, DateTime? StartDate, DateTime? EndDate, string? RentalMode);
public record DispatchFleetStatusResponse(
    List<DispatchVehicleStatusResponse> Vehicles, List<DispatchDriverStatusResponse> Drivers);
public record DispatchAssignableResponse(List<VehicleResponse> Vehicles, List<DriverResponse> Drivers);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record CreateVehicleTypeRequest(string TypeName, decimal PricePerDay, decimal PricePerKm);
public record AdminDriverResponse(int DriverId, string FullName, string Email, string? Phone, string LicenseNumber, DateOnly LicenseExpiry, string Status, decimal AverageRating, int TotalTrips, bool IsActive);
public record CreateAdminDriverRequest(string FullName, string Email, string Phone, string Password);
public record UpdateAdminDriverRequest(string? FullName, string? Phone, string? Status);
public record AdminCustomerResponse(int CustomerId, string FullName, string Email, string? Phone, bool IsLocked, DateTime CreatedAt, bool IsActive = true, string? Address = null, string? IdNumber = null, DateOnly? DateOfBirth = null, string? LockReason = null, DateTime? LockedAt = null, int? LockedByUserId = null, string? LockedByName = null, string? InactiveReason = null, DateTime? InactivatedAt = null, int? InactivatedByUserId = null, string? InactivatedByName = null);
public record AdminCustomerListResponse(List<AdminCustomerResponse> Items, int Total, int Page, int PageSize);
public record LockCustomerRequest(bool IsLocked, string? Reason = null);
public record DeactivateCustomerRequest(string? Reason);
public record CreateAdminCustomerRequest(string FullName, string Email, string Phone, string Password, string? Address, string? IdNumber, DateOnly? DateOfBirth);
public record UpdateAdminCustomerRequest(string FullName, string Email, string Phone, string? Address, string? IdNumber, DateOnly? DateOfBirth);
