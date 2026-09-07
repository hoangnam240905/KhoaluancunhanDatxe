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

public static class RentalModes
{
    public const string WithDriver = "WithDriver";
    public const string SelfDrive = "SelfDrive";

    public static bool TryResolve(string? rentalMode, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(rentalMode))
        {
            resolved = WithDriver;
            return true;
        }

        var trimmed = rentalMode.Trim();
        if (trimmed == WithDriver)
        {
            resolved = WithDriver;
            return true;
        }

        if (trimmed == SelfDrive)
        {
            resolved = SelfDrive;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
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

    public static bool IsOpen(string? status) =>
        status is Assigned or Accepted or InProgress;
}

/// Defaults applied only when VehicleType mode-pricing columns are NULL.
/// Keeps legacy TotalAmount formula when DriverFee=0 and SelfDrive included km=0.
public static class PricingDefaults
{
    public const decimal IncludedKmPerDay = 200;

    public static decimal DriverFeePerDay(decimal pricePerDay) =>
        Math.Round(pricePerDay * 0.5m, 0, MidpointRounding.AwayFromZero);

    public static decimal SelfDrivePricePerDay(decimal pricePerDay) => pricePerDay;

    public static decimal ExtraKmPrice(decimal pricePerKm) => pricePerKm;

    public static decimal WithDriverDeposit(decimal pricePerDay) => pricePerDay;

    public static decimal SelfDriveDeposit(decimal pricePerDay) => pricePerDay * 5;
}

public static class PaymentTypes
{
    public const string Deposit = "Deposit";
    public const string Balance = "Balance";
    public const string Refund = "Refund";

    public static bool TryResolve(string? paymentType, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(paymentType))
        {
            resolved = string.Empty;
            return false;
        }

        var trimmed = paymentType.Trim();
        if (trimmed is Deposit or Balance or Refund)
        {
            resolved = trimmed;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string BankTransfer = "BankTransfer";
    public const string MoMo = "MoMo";
    public const string VNPay = "VNPay";

    public static bool TryResolve(string? method, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            resolved = string.Empty;
            return false;
        }

        var trimmed = method.Trim();
        if (trimmed is Cash or BankTransfer or MoMo or VNPay)
        {
            resolved = trimmed;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Refunded = "Refunded";
}

public static class ContractStatuses
{
    public const string Issued = "Issued";
    public const string Signed = "Signed";
    public const string Voided = "Voided";
}

public static class IncidentTypes
{
    public const string Accident = "Accident";
    public const string VehicleIssue = "VehicleIssue";
    public const string CustomerIssue = "CustomerIssue";
    public const string Other = "Other";

    public static bool TryResolve(string? incidentType, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(incidentType))
        {
            resolved = string.Empty;
            return false;
        }

        var trimmed = incidentType.Trim();
        if (trimmed is Accident or VehicleIssue or CustomerIssue or Other)
        {
            resolved = trimmed;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}

public static class IncidentStatuses
{
    public const string Open = "Open";
}

public static class MaintenanceTypes
{
    public const string Scheduled = "Scheduled";
    public const string Repair = "Repair";
    public const string Inspection = "Inspection";

    public static bool TryResolve(string? maintenanceType, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(maintenanceType))
        {
            resolved = string.Empty;
            return false;
        }

        var trimmed = maintenanceType.Trim();
        if (trimmed is Scheduled or Repair or Inspection)
        {
            resolved = trimmed;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}

public static class MaintenanceAlertThresholds
{
    public const int KmThreshold = 5000;
    public const int DaysThreshold = 180;
}

public static class MaintenanceLock
{
    public const string BlockedForNewSchedule = "Xe đã đến hạn bảo trì, không nhận lịch mới.";
}

public static class RecommendationWeights
{
    public const decimal Rating = 0.5m;
    public const decimal BookingCount = 0.3m;
    public const decimal Availability = 0.2m;
}

/// <summary>
/// Technical buffer between consecutive trips of the same vehicle/driver.
/// Thesis spec: T_buffer = 2 hours.
/// </summary>
public static class ScheduleBuffers
{
    public const int TechnicalHours = 2;
    public static readonly TimeSpan Technical = TimeSpan.FromHours(TechnicalHours);
}

public static class ScheduleConflictTypes
{
    public const string Vehicle = "Vehicle";
    public const string Driver = "Driver";
}

public static class VehicleInspectionTypes
{
    public const string Handover = "Handover";
    public const string Return = "Return";

    public static bool TryResolve(string? inspectionType, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(inspectionType))
        {
            resolved = string.Empty;
            return false;
        }

        var trimmed = inspectionType.Trim();
        if (trimmed is Handover or Return)
        {
            resolved = trimmed;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}

public static class BookingFeeTypes
{
    public const string LateFee = "LateFee";
    public const string ExtraKm = "ExtraKm";
    public const string Fuel = "Fuel";
    public const string Damage = "Damage";
    public const string Other = "Other";

    public static bool IsIncludedInBase(string? feeType) =>
        feeType == ExtraKm;

    public static bool TryResolve(string? feeType, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(feeType))
        {
            resolved = string.Empty;
            return false;
        }

        var trimmed = feeType.Trim();
        if (trimmed is LateFee or ExtraKm or Fuel or Damage or Other)
        {
            resolved = trimmed;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}
