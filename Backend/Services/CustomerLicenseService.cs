using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Customers;
using Backend.DTOs.Licences;
using Backend.Entities;
using Backend.Services.Notifications;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Self-drive customer's driving licences (plan1.md Phần E, Phase 9: several per customer). The customer declares
/// each licence and sends both sides; a dispatcher approves each licence once — not per booking — and the original
/// is still checked at handover. A vehicle type may be booked self-drive when ANY licence covers its class and is
/// valid through the return date; confirming the booking needs such a licence to be approved.
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

    /// <summary>Why this customer cannot book this type self-drive until the return date; null when they can (pending licences count).</summary>
    public static string? SelfDriveBlockReason(Customer customer, VehicleType type, DateTime tripEnd)
        => SelfDriveBlockReason(LicenceBook.Of(customer), type, tripEnd);

    public static string? SelfDriveBlockReason(IReadOnlyList<PersonLicense> licences, VehicleType type, DateTime tripEnd)
    {
        var declared = licences.Where(l => !string.IsNullOrWhiteSpace(l.Number)).ToList();
        if (declared.Count == 0)
            return ProvideLicence(type);
        var usable = declared.Where(l => l.Status != PersonLicenseStatuses.Rejected).ToList();
        if (usable.Count == 0)
            return Rejected;
        var end = DateOnly.FromDateTime(tripEnd);
        var covering = usable.Where(l => Covers(l, type)).ToList();
        if (covering.Any(l => l.Expiry >= end))
            return null;
        if (covering.Count > 0)
            return ExpiresBeforeReturn;
        var widest = usable.OrderByDescending(l => LicenseClasses.Find(l.Class)?.MaxSeats ?? 0).First();
        return LicenseClasses.CannotDrive(widest.Class, type.MinLicenseClass, type.SeatCapacity, professional: false);
    }

    /// <summary>Dispatcher confirm / handover of a self-drive booking: an approved licence must cover the type until the return date.</summary>
    public static string? ConfirmBlockReason(Customer customer, VehicleType type, DateTime tripEnd)
        => ConfirmBlockReason(LicenceBook.Of(customer), type, tripEnd);

    public static string? ConfirmBlockReason(IReadOnlyList<PersonLicense> licences, VehicleType type, DateTime tripEnd)
    {
        if (SelfDriveBlockReason(licences, type, tripEnd) is string reason)
            return reason;
        var end = DateOnly.FromDateTime(tripEnd);
        return licences.Any(l => l.Status == PersonLicenseStatuses.Approved && Covers(l, type) && l.Expiry >= end)
            ? null
            : AwaitingApproval;
    }

    private static bool Covers(PersonLicense l, VehicleType type)
        => LicenseClasses.CannotDrive(l.Class, type.MinLicenseClass, type.SeatCapacity, professional: false) is null;

    public async Task<CustomerLicenseResponse?> GetAsync(int customerId, DateTime? tripEnd = null)
    {
        var customer = await db.Customers.AsNoTracking().Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return null;
        await LicenceBook.LoadAsync(db, customer);
        return await ToResponseAsync(customer, tripEnd);
    }

    /// <summary>Dispatcher list: customers with a licence that is Pending (default) / Approved / Rejected, or All declared.</summary>
    public async Task<List<CustomerLicenseResponse>> ListAsync(string? status)
    {
        var wanted = string.IsNullOrWhiteSpace(status) ? PersonLicenseStatuses.Pending : status.Trim();
        var all = string.Equals(wanted, "All", StringComparison.OrdinalIgnoreCase);
        var withRows = await db.PersonLicenses.AsNoTracking()
            .Where(l => l.OwnerKind == LicenceOwnerKinds.Customer).Select(l => l.OwnerId).Distinct().ToListAsync();
        var rows = await db.Customers.AsNoTracking().Include(c => c.User)
            .Where(c => c.LicenseStatus != CustomerLicenseStatuses.None || withRows.Contains(c.CustomerId))
            .OrderBy(c => c.CustomerId).ToListAsync();
        var result = new List<CustomerLicenseResponse>();
        foreach (var c in rows)
        {
            await LicenceBook.LoadAsync(db, c);
            var licences = LicenceBook.Of(c);
            if (licences.Count == 0 || (!all && !licences.Any(l => l.Status == wanted)))
                continue;
            result.Add(await ToResponseAsync(c, null));
        }
        return result;
    }

    /// <summary>Declare the first licence or correct the primary one (older single-licence screens).</summary>
    public async Task<(CustomerLicenseResponse? Data, string? Error)> UpdateAsync(int customerId, UpdateCustomerLicenseRequest request)
    {
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, customer);
        return await SaveAsync(customer, rows, rows.FirstOrDefault(), request);
    }

    /// <summary>Phase 9: add another licence (e.g. D1 next to B). Other licences keep their status.</summary>
    public async Task<(CustomerLicenseResponse? Data, string? Error)> AddLicenceAsync(int customerId, UpdateCustomerLicenseRequest request)
    {
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, customer);
        return await SaveAsync(customer, rows, null, request);
    }

    /// <summary>Phase 9: correct one licence; only that licence goes back to "Chờ duyệt".</summary>
    public async Task<(CustomerLicenseResponse? Data, string? Error)> UpdateLicenceAsync(int customerId, int licenseId, UpdateCustomerLicenseRequest request)
    {
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, customer);
        var licence = rows.FirstOrDefault(l => l.PersonLicenseId == licenseId);
        return licence is null ? (null, LicenceBook.NotFound) : await SaveAsync(customer, rows, licence, request);
    }

    private async Task<(CustomerLicenseResponse? Data, string? Error)> SaveAsync(
        Customer customer, List<PersonLicense> rows, PersonLicense? licence, UpdateCustomerLicenseRequest request)
    {
        var number = LicenseNumberRules.Normalize(request.LicenseNumber);
        if (number is null)
            return (null, NumberRequired);
        if (!LicenseNumberRules.IsValid(number))
            return (null, LicenseNumberRules.Invalid);
        if (number != licence?.Number
            && await LicenceBook.NumberProblemAsync(db, number, LicenceOwnerKinds.Customer, customer.CustomerId, licence?.PersonLicenseId) is string numberError)
            return (null, numberError);
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

        if (licence is null)
        {
            licence = new PersonLicense
            {
                OwnerKind = LicenceOwnerKinds.Customer, OwnerId = customer.CustomerId,
                Status = PersonLicenseStatuses.Pending, CreatedAt = DateTime.UtcNow
            };
            db.PersonLicenses.Add(licence);
            rows.Add(licence);
        }
        var changed = licence.Number != number || licence.Class != cls.Code
            || licence.Expiry != expiry || licence.IssueDate != request.LicenseIssueDate;
        licence.Number = number;
        licence.Class = cls.Code;
        licence.Expiry = expiry;
        licence.IssueDate = request.LicenseIssueDate;
        if (changed)
        {
            // A new or corrected licence is approved again before it counts for a self-drive confirmation.
            licence.Status = PersonLicenseStatuses.Pending;
            licence.ReviewedByUserId = null;
            licence.ReviewedByName = null;
            licence.ReviewedAt = null;
            licence.ReviewNote = null;
        }
        LicenceBook.Mirror(customer, rows);
        await db.SaveChangesAsync();
        await NotifyDispatchersIfReadyAsync(customer.CustomerId);
        return (await ToResponseAsync(customer, null), null);
    }

    /// <summary>DP-1: a pending licence with both photos is ready for review.</summary>
    public async Task NotifyDispatchersIfReadyAsync(int customerId)
    {
        var customer = await db.Customers.AsNoTracking().Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return;
        await LicenceBook.LoadAsync(db, customer);
        var licences = LicenceBook.Of(customer);
        var ready = false;
        foreach (var l in licences.Where(l => l.Status == PersonLicenseStatuses.Pending && !string.IsNullOrWhiteSpace(l.Number)))
        {
            var current = await PhotosOfAsync(customerId, licences, l);
            if (UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Customer), out _) == "Pending")
                ready = true;
        }
        if (!ready)
            return;
        var message = $"Khách {customer.User.FullName} vừa gửi ảnh bằng lái, cần duyệt.";
        var since = DateTime.UtcNow.AddMinutes(-10);
        if (await db.Notifications.AnyAsync(n => n.Message == message && n.CreatedAt >= since))
            return;
        await UserNotifications.AddToDispatchersAsync(db, "Hồ sơ bằng lái cần duyệt", message);
        await db.SaveChangesAsync();
    }

    /// <summary>Review the first licence still waiting (else the primary) — older single-licence pages.</summary>
    public async Task<(CustomerLicenseResponse? Data, string? Error)> ReviewAsync(int customerId, int dispatcherId, bool approve, string? reason)
    {
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, customer);
        var target = rows.FirstOrDefault(l => l.Status == PersonLicenseStatuses.Pending) ?? rows.FirstOrDefault();
        if (target is null)
            return (null, InfoIncomplete);
        return await ReviewCoreAsync(customer, rows, target, dispatcherId, approve, reason);
    }

    /// <summary>Phase 9: approve (KH-1) or reject with a reason (KH-2) one licence; its photos follow the same decision.</summary>
    public async Task<(CustomerLicenseResponse? Data, string? Error)> ReviewLicenceAsync(
        int customerId, int licenseId, int dispatcherId, bool approve, string? reason)
    {
        var customer = await db.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, customer);
        var target = rows.FirstOrDefault(l => l.PersonLicenseId == licenseId);
        return target is null
            ? (null, LicenceBook.NotFound)
            : await ReviewCoreAsync(customer, rows, target, dispatcherId, approve, reason);
    }

    private async Task<(CustomerLicenseResponse? Data, string? Error)> ReviewCoreAsync(
        Customer customer, List<PersonLicense> rows, PersonLicense licence, int dispatcherId, bool approve, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!approve && text is null)
            return (null, ReasonRequired);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong);

        var current = await PhotosOfAsync(customer.CustomerId, rows, licence);
        if (approve)
        {
            if (string.IsNullOrWhiteSpace(licence.Number) || licence.Expiry == DateOnly.MinValue)
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
        licence.Status = approve ? PersonLicenseStatuses.Approved : PersonLicenseStatuses.Rejected;
        licence.ReviewNote = approve ? null : text;
        licence.ReviewedByUserId = actor?.UserId;
        licence.ReviewedByName = actor?.Name;
        licence.ReviewedAt = DateTime.UtcNow;
        LicenceBook.Mirror(customer, rows);
        var label = rows.Count > 1 ? $" hạng {LicenseClasses.Label(licence.Class)}" : "";
        if (approve)
            UserNotifications.Add(db, customer.CustomerId, $"Bằng lái{label} đã được chấp nhận",
                "Lúc nhận xe, vui lòng mang bằng lái gốc để xác minh.");
        else
            UserNotifications.Add(db, customer.CustomerId, $"Bằng lái{label} chưa được chấp nhận",
                $"Lý do: {text}. Vui lòng gửi lại ảnh bằng lái hoặc liên hệ điều phối.");
        await db.SaveChangesAsync();
        return (await ToResponseAsync(customer, null), null);
    }

    /// <summary>The current photos of one licence (untagged photos belong to the primary licence).</summary>
    private Task<List<DTOs.Documents.UserDocumentResponse>> PhotosOfAsync(int customerId, IReadOnlyList<PersonLicense> rows, PersonLicense licence)
        => documents.GetCurrentAsync(customerId, licence.PersonLicenseId == 0 ? null : licence.PersonLicenseId, LicenceBook.IsPrimary(rows, licence));

    private async Task<CustomerLicenseResponse> ToResponseAsync(Customer c, DateTime? tripEnd)
    {
        var licences = LicenceBook.Of(c);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = new List<PersonLicenseResponse>();
        foreach (var l in licences)
        {
            var docs = await PhotosOfAsync(c.CustomerId, licences, l);
            var ps = UserDocumentService.PhotoStatus(docs, UserDocumentService.RequiredFor(RoleNames.Customer), out var miss);
            items.Add(new PersonLicenseResponse(l.PersonLicenseId, l.Number, l.Class, LicenseClasses.Label(l.Class), l.IssueDate,
                l.Expiry, l.Status, ps, miss, docs, l.ReviewedByName, l.ReviewedAt, l.ReviewNote, LicenceBook.IsPrimary(licences, l),
                l.Expiry >= today && l.Expiry < today.AddDays(30), l.Expiry < today));
        }

        var current = items.FirstOrDefault()?.Documents.ToList() ?? await documents.GetCurrentAsync(c.CustomerId);
        var photoStatus = UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Customer), out var missing);
        var end = tripEnd ?? DateTime.Today;
        var types = await db.VehicleTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SeatCapacity).ToListAsync();
        var eligibility = types.Select(t =>
        {
            var reason = SelfDriveBlockReason(licences, t, end);
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
            eligibility, items);
    }
}
