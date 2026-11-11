using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Customers;
using Backend.Entities;
using Backend.Services.Notifications;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Self-drive customer's driving licence (plan1.md Phần E). The customer declares the licence and sends both
/// sides; the class decides which vehicle types they may book self-drive (Luật 36/2024, old licences still
/// valid accepted). A dispatcher approves it once per customer — not per booking — and the original is still
/// checked at handover. Bookings can be placed while the licence waits for approval; confirming them cannot.
/// </summary>
public class CustomerLicenseService(CarRentalDbContext db, UserDocumentService documents)
{
    public const string NotFound = "Không tìm thấy khách hàng.";
    public const string NumberRequired = "Vui lòng nhập số bằng lái.";
    public const string NumberTooLong = "Số bằng lái tối đa 30 ký tự.";
    public const string ClassInvalid = "Hạng bằng không hợp lệ.";
    public const string MotorcycleLicence = "Bằng B1 cấp từ 01/01/2025 là bằng mô tô, không lái được ô tô.";
    public const string ExpiryRequired = "Vui lòng nhập ngày hết hạn của bằng lái.";
    public const string Expired = "Bằng lái đã hết hạn.";
    public const string IssueDateInvalid = "Ngày cấp phải trước ngày hết hạn và không ở tương lai.";
    public const string ExpiresBeforeReturn = "Bằng lái hết hạn trước ngày trả xe, không thể đặt xe tự lái.";
    public const string Rejected = "Bằng lái chưa được chấp nhận. Vui lòng gửi lại ảnh bằng lái hoặc liên hệ điều phối.";
    public const string AwaitingApproval = "Chờ duyệt bằng lái: điều phối cần duyệt bằng lái của khách trước khi xác nhận đơn tự lái.";
    public const string InfoIncomplete = "Khách chưa khai đủ thông tin bằng lái.";
    public const string PhotosIncomplete = "Khách chưa gửi đủ ảnh bằng lái (mặt trước và mặt sau).";
    public const string ReasonRequired = "Vui lòng ghi lý do.";
    public const string ReasonTooLong = "Lý do tối đa 500 ký tự.";

    /// <summary>KH-0: what to show when a customer without a usable licence picks a vehicle type self-drive.</summary>
    public static string ProvideLicence(VehicleType type)
    {
        var minimum = MinimumFor(type);
        return $"Hãy cung cấp bằng lái phù hợp với xe bạn chọn (xe {type.SeatCapacity} chỗ cần bằng {minimum} trở lên).";
    }

    public static string MinimumFor(VehicleType type)
        => LicenseClasses.VehicleMinimums.Contains(type.MinLicenseClass ?? "")
            ? type.MinLicenseClass!
            : LicenseClasses.MinimumForSeats(type.SeatCapacity);

    /// <summary>Why this customer cannot book this type self-drive until the return date; null when they can.</summary>
    public static string? SelfDriveBlockReason(Customer customer, VehicleType type, DateTime tripEnd)
    {
        if (customer.LicenseStatus == CustomerLicenseStatuses.None || string.IsNullOrWhiteSpace(customer.DriverLicenseNumber))
            return ProvideLicence(type);
        if (customer.LicenseStatus == CustomerLicenseStatuses.Rejected)
            return Rejected;
        if (LicenseClasses.CannotDrive(customer.DriverLicenseClass, type.MinLicenseClass, type.SeatCapacity, professional: false) is string cannot)
            return cannot;
        if (customer.DriverLicenseExpiry is not DateOnly expiry || expiry < DateOnly.FromDateTime(tripEnd))
            return ExpiresBeforeReturn;
        return null;
    }

    /// <summary>Dispatcher confirm of a self-drive booking: the licence must be approved and still fit the trip.</summary>
    public static string? ConfirmBlockReason(Customer customer, VehicleType type, DateTime tripEnd)
        => SelfDriveBlockReason(customer, type, tripEnd)
           ?? (customer.LicenseStatus == CustomerLicenseStatuses.Approved ? null : AwaitingApproval);

    public async Task<CustomerLicenseResponse?> GetAsync(int customerId, DateTime? tripEnd = null)
    {
        var customer = await db.Customers.AsNoTracking().Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        return customer is null ? null : await ToResponseAsync(customer, tripEnd);
    }

    /// <summary>Dispatcher list: status Pending (default) / Approved / Rejected / All (declared licences only).</summary>
    public async Task<List<CustomerLicenseResponse>> ListAsync(string? status)
    {
        var wanted = string.IsNullOrWhiteSpace(status) ? CustomerLicenseStatuses.Pending : status.Trim();
        var query = db.Customers.AsNoTracking().Include(c => c.User).Where(c => c.LicenseStatus != CustomerLicenseStatuses.None);
        if (!string.Equals(wanted, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => c.LicenseStatus == wanted);
        var rows = await query.OrderBy(c => c.CustomerId).ToListAsync();
        var result = new List<CustomerLicenseResponse>();
        foreach (var c in rows)
            result.Add(await ToResponseAsync(c, null));
        return result;
    }

    public async Task<(CustomerLicenseResponse? Data, string? Error)> UpdateAsync(int customerId, UpdateCustomerLicenseRequest request)
    {
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);
        var number = LicenseNumberRules.Normalize(request.LicenseNumber);
        if (number is null)
            return (null, NumberRequired);
        if (!LicenseNumberRules.IsValid(number))
            return (null, LicenseNumberRules.Invalid);
        if (number != customer.DriverLicenseNumber
            && await db.Customers.AnyAsync(c => c.CustomerId != customerId && c.DriverLicenseNumber == number))
            return (null, LicenseNumberRules.Taken);
        var cls = LicenseClasses.Find(request.LicenseClass);
        if (cls is null)
            return (null, ClassInvalid);
        if (cls.MaxSeats == 0)
            return (null, MotorcycleLicence);
        if (request.LicenseExpiry is not DateOnly expiry)
            return (null, ExpiryRequired);
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (expiry < today)
            return (null, Expired);
        if (request.LicenseIssueDate is DateOnly issued && (issued > today || issued >= expiry))
            return (null, IssueDateInvalid);

        var changed = customer.DriverLicenseNumber != number || customer.DriverLicenseClass != cls.Code
            || customer.DriverLicenseExpiry != expiry || customer.DriverLicenseIssueDate != request.LicenseIssueDate;
        customer.DriverLicenseNumber = number;
        customer.DriverLicenseClass = cls.Code;
        customer.DriverLicenseExpiry = expiry;
        customer.DriverLicenseIssueDate = request.LicenseIssueDate;
        if (changed || customer.LicenseStatus == CustomerLicenseStatuses.None)
        {
            // A new or corrected licence is approved again before the next self-drive confirmation.
            customer.LicenseStatus = CustomerLicenseStatuses.Pending;
            customer.LicenseReviewedByUserId = null;
            customer.LicenseReviewedByName = null;
            customer.LicenseReviewedAt = null;
            customer.LicenseReviewNote = null;
        }
        await db.SaveChangesAsync();
        await NotifyDispatchersIfReadyAsync(customerId);
        return (await ToResponseAsync(customer, null), null);
    }

    /// <summary>DP-1: a pending licence with both photos is ready for review.</summary>
    public async Task NotifyDispatchersIfReadyAsync(int customerId)
    {
        var customer = await db.Customers.AsNoTracking().Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null || customer.LicenseStatus != CustomerLicenseStatuses.Pending || string.IsNullOrWhiteSpace(customer.DriverLicenseNumber))
            return;
        var current = await documents.GetCurrentAsync(customerId);
        if (UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Customer), out _) != "Pending")
            return;
        var message = $"Khách {customer.User.FullName} vừa gửi ảnh bằng lái, cần duyệt.";
        var since = DateTime.UtcNow.AddMinutes(-10);
        if (await db.Notifications.AnyAsync(n => n.Message == message && n.CreatedAt >= since))
            return;
        await UserNotifications.AddToDispatchersAsync(db, "Hồ sơ bằng lái cần duyệt", message);
        await db.SaveChangesAsync();
    }

    /// <summary>Dispatcher approves (KH-1) or rejects with a reason (KH-2); the photos follow the same decision.</summary>
    public async Task<(CustomerLicenseResponse? Data, string? Error)> ReviewAsync(int customerId, int dispatcherId, bool approve, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!approve && text is null)
            return (null, ReasonRequired);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong);
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);

        var current = await documents.GetCurrentAsync(customerId);
        if (approve)
        {
            if (string.IsNullOrWhiteSpace(customer.DriverLicenseNumber) || customer.DriverLicenseExpiry is null)
                return (null, InfoIncomplete);
            if (UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Customer), out _) is "Missing" or "Rejected")
                return (null, PhotosIncomplete);
        }
        foreach (var doc in current.Where(d => d.Status == UserDocumentStatuses.Pending))
        {
            var (_, error, _) = await documents.ReviewAsync(doc.UserDocumentId, dispatcherId, approve, text);
            if (error is not null)
                return (null, error);
        }

        var actor = await ActorLookup.FindAsync(db, dispatcherId);
        customer.LicenseStatus = approve ? CustomerLicenseStatuses.Approved : CustomerLicenseStatuses.Rejected;
        customer.LicenseReviewNote = approve ? null : text;
        customer.LicenseReviewedByUserId = actor?.UserId;
        customer.LicenseReviewedByName = actor?.Name;
        customer.LicenseReviewedAt = DateTime.UtcNow;
        if (approve)
            UserNotifications.Add(db, customerId, "Bằng lái đã được chấp nhận",
                "Lúc nhận xe, vui lòng mang bằng lái gốc để xác minh.");
        else
            UserNotifications.Add(db, customerId, "Bằng lái chưa được chấp nhận",
                $"Lý do: {text}. Vui lòng gửi lại ảnh bằng lái hoặc liên hệ điều phối.");
        await db.SaveChangesAsync();
        return (await ToResponseAsync(customer, null), null);
    }

    private async Task<CustomerLicenseResponse> ToResponseAsync(Customer c, DateTime? tripEnd)
    {
        var current = await documents.GetCurrentAsync(c.CustomerId);
        var photoStatus = UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Customer), out var missing);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var end = tripEnd ?? DateTime.Today;
        var types = await db.VehicleTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SeatCapacity).ToListAsync();
        var eligibility = types.Select(t =>
        {
            var reason = SelfDriveBlockReason(c, t, end);
            return new SelfDriveTypeEligibility(t.TypeId, t.TypeName, t.SeatCapacity, MinimumFor(t), reason is null, reason);
        }).ToList();
        return new CustomerLicenseResponse(
            c.CustomerId, c.User.FullName, c.User.Email, c.User.Phone,
            c.DriverLicenseNumber, c.DriverLicenseClass,
            c.DriverLicenseClass is null ? null : LicenseClasses.Label(c.DriverLicenseClass),
            c.DriverLicenseIssueDate, c.DriverLicenseExpiry, c.LicenseStatus, photoStatus, missing, current,
            c.LicenseReviewedByName, c.LicenseReviewedAt, c.LicenseReviewNote,
            c.DriverLicenseExpiry is DateOnly e1 && e1 >= today && e1 < today.AddDays(30),
            c.DriverLicenseExpiry is DateOnly e2 && e2 < today,
            eligibility);
    }
}
