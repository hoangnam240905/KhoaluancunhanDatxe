using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Documents;
using Backend.DTOs.Drivers;
using Backend.Entities;
using Backend.Services.Notifications;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Driver licence verification (plan1.md Phần D). The driver declares the licence and sends photos of the
/// licence and ID card; a dispatcher first accepts the photos (step 1, online), then checks the original
/// at the office (step 2). Only verified drivers can be assigned (see <see cref="DriverQualification"/>).
/// Every review records the dispatcher's name; the driver is told by inbox notification (texts in plan1.md D.6).
/// </summary>
public class DriverVerificationService(
    CarRentalDbContext db,
    UserDocumentService documents,
    IConfiguration configuration)
{
    public const string PhotoMissing = "Missing";
    public const string PhotoPending = "Pending";
    public const string PhotoAccepted = "Accepted";
    public const string PhotoRejected = "Rejected";

    public const string NotFound = "Không tìm thấy tài xế.";
    public const string LicenseNumberRequired = "Vui lòng nhập số bằng lái.";
    public const string LicenseNumberTooLong = "Số bằng lái tối đa 30 ký tự.";
    public const string LicenseClassInvalid = "Hạng bằng không hợp lệ.";
    public const string LicenseClassNotForDrivers = "Hạng bằng này không dùng được để lái xe cho công ty (cần B trở lên, không dùng B1).";
    public const string ExpiryRequired = "Vui lòng nhập ngày hết hạn của bằng lái.";
    public const string LicenseAlreadyExpired = "Bằng lái đã hết hạn.";
    public const string IssueDateInFuture = "Ngày cấp không được ở tương lai.";
    public const string IssueAfterExpiry = "Ngày cấp phải trước ngày hết hạn.";
    public const string IdNumberInvalid = "Số CCCD phải gồm 12 chữ số.";
    public const string InfoIncomplete = "Tài xế chưa khai đủ thông tin bằng lái.";
    public const string PhotosIncomplete = "Tài xế chưa gửi đủ ảnh bằng lái và CCCD (mặt trước và mặt sau).";
    public const string NothingToReview = "Không có ảnh nào đang chờ duyệt.";
    public const string PhotosNotAccepted = "Cần chấp nhận ảnh bằng lái trước khi xác minh bằng gốc.";
    public const string ReasonRequired = "Vui lòng ghi lý do.";
    public const string ReasonTooLong = "Lý do tối đa 500 ký tự.";
    public const string AlreadyVerified = "Tài xế đã được xác minh.";

    private int OfficeVisitDays => configuration.GetValue("LicenseVerification:OfficeVisitDays", 7);

    public static IReadOnlyList<LicenseClassOption> ClassOptions()
        => LicenseClasses.All.Select(c => new LicenseClassOption(c.Code, c.Label, c.MaxSeats, c.Professional && c.MaxSeats > 0)).ToList();

    /// <summary>TX-0: the guidance a new driver gets right after signing up.</summary>
    public static void AddWelcomeNotification(CarRentalDbContext db, int driverId)
        => UserNotifications.Add(db, driverId, "Cập nhật bằng lái và giấy tờ",
            "Vui lòng nhập thông tin bằng lái và gửi ảnh bằng lái, CCCD (mặt trước và mặt sau) để được xác minh.");

    public async Task<DriverVerificationResponse?> GetAsync(int driverId)
    {
        var driver = await db.Drivers.AsNoTracking().Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        return driver is null ? null : await ToResponseAsync(driver);
    }

    /// <summary>Dispatcher list; status "Pending" = still to verify (the default), "Verified", "Rejected", or "All".</summary>
    public async Task<List<DriverVerificationResponse>> ListAsync(string? status)
    {
        var query = db.Drivers.AsNoTracking().Include(d => d.User).Where(d => d.IsActive);
        var wanted = string.IsNullOrWhiteSpace(status) ? DriverVerificationStatuses.Pending : status.Trim();
        if (!string.Equals(wanted, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(d => d.VerificationStatus == wanted);
        var rows = await query.OrderBy(d => d.DriverId).ToListAsync();
        var result = new List<DriverVerificationResponse>();
        foreach (var driver in rows)
            result.Add(await ToResponseAsync(driver));
        return result;
    }

    /// <summary>The driver enters or corrects their licence. Changing a verified licence sends it back to "Chờ xác minh".</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> UpdateLicenseAsync(int driverId, UpdateDriverLicenseRequest request)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);

        var number = request.LicenseNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(number))
            return (null, LicenseNumberRequired);
        if (number.Length > 30)
            return (null, LicenseNumberTooLong);
        var cls = LicenseClasses.Find(request.LicenseClass);
        if (cls is null)
            return (null, LicenseClassInvalid);
        if (!cls.Professional || cls.MaxSeats == 0)
            return (null, LicenseClassNotForDrivers);
        if (request.LicenseExpiry is not DateOnly expiry)
            return (null, ExpiryRequired);
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (expiry < today)
            return (null, LicenseAlreadyExpired);
        if (request.LicenseIssueDate is DateOnly issued)
        {
            if (issued > today)
                return (null, IssueDateInFuture);
            if (issued >= expiry)
                return (null, IssueAfterExpiry);
        }
        var idNumber = string.IsNullOrWhiteSpace(request.IdNumber) ? null : request.IdNumber.Trim();
        if (idNumber is not null && (idNumber.Length != 12 || !idNumber.All(char.IsDigit)))
            return (null, IdNumberInvalid);

        var changed = driver.LicenseNumber != number || driver.LicenseClass != cls.Code
            || driver.LicenseExpiry != expiry || driver.LicenseIssueDate != request.LicenseIssueDate
            || (idNumber is not null && driver.IdNumber != idNumber);
        driver.LicenseNumber = number;
        driver.LicenseClass = cls.Code;
        driver.LicenseExpiry = expiry;
        driver.LicenseIssueDate = request.LicenseIssueDate;
        if (idNumber is not null)
            driver.IdNumber = idNumber;
        if (changed && driver.VerificationStatus != DriverVerificationStatuses.Pending)
        {
            // A new or corrected licence must be checked again before the next assignment.
            driver.VerificationStatus = DriverVerificationStatuses.Pending;
            driver.VerifiedByUserId = null;
            driver.VerifiedByName = null;
            driver.VerifiedAt = null;
        }
        driver.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await NotifyDispatchersIfReadyAsync(driverId);
        return (await ToResponseAsync(driver), null);
    }

    /// <summary>DP-1: once a pending driver has declared the licence and sent every photo, tell the dispatchers.</summary>
    public async Task NotifyDispatchersIfReadyAsync(int driverId)
    {
        var driver = await db.Drivers.AsNoTracking().Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null || driver.VerificationStatus != DriverVerificationStatuses.Pending || !InfoComplete(driver))
            return;
        var current = await documents.GetCurrentAsync(driverId);
        if (PhotoStatusOf(current, out _) != PhotoPending)
            return;

        var message = $"Tài xế {driver.User.FullName} vừa gửi ảnh bằng lái, cần xác minh.";
        var since = DateTime.UtcNow.AddMinutes(-10);
        if (await db.Notifications.AnyAsync(n => n.Message == message && n.CreatedAt >= since))
            return; // several photos sent one after another → one notification
        await UserNotifications.AddToDispatchersAsync(db, "Hồ sơ bằng lái cần duyệt", message);
        await db.SaveChangesAsync();
    }

    /// <summary>Step 1 (online): accept every pending photo (TX-1) or reject them with a reason (TX-2).</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> ReviewPhotosAsync(
        int driverId, int dispatcherId, bool accept, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!accept && text is null)
            return (null, ReasonRequired);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong);
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        if (driver.VerificationStatus == DriverVerificationStatuses.Verified)
            return (null, AlreadyVerified);

        var current = await documents.GetCurrentAsync(driverId);
        if (accept)
        {
            if (!InfoComplete(driver))
                return (null, InfoIncomplete);
            if (PhotoStatusOf(current, out _) is PhotoMissing or PhotoRejected)
                return (null, PhotosIncomplete);
        }
        var pending = current.Where(d => d.Status == UserDocumentStatuses.Pending).ToList();
        if (pending.Count == 0)
            return (null, NothingToReview);

        foreach (var doc in pending)
        {
            var (_, error, _) = await documents.ReviewAsync(doc.UserDocumentId, dispatcherId, accept, text);
            if (error is not null)
                return (null, error);
        }

        if (accept)
            UserNotifications.Add(db, driverId, "Bằng lái đã được chấp nhận",
                $"Vui lòng lên văn phòng trong vòng {OfficeVisitDays} ngày tới để xác nhận bằng gốc. Mang theo bằng lái và CCCD. Sau khi xác minh xong, bạn sẽ được phân công chuyến.");
        else
            UserNotifications.Add(db, driverId, "Ảnh bằng lái chưa đạt",
                $"Lý do: {text}. Vui lòng chụp lại ảnh rõ hơn và gửi lại.");
        await db.SaveChangesAsync();
        return (await ToResponseAsync(driver), null);
    }

    /// <summary>Step 2 (at the office): the original licence matches → Verified (TX-3); otherwise Rejected with a reason (TX-4).</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> VerifyAsync(
        int driverId, int dispatcherId, bool verified, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!verified && text is null)
            return (null, ReasonRequired);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong);
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);

        if (verified)
        {
            if (driver.VerificationStatus == DriverVerificationStatuses.Verified)
                return (null, AlreadyVerified);
            if (!InfoComplete(driver))
                return (null, InfoIncomplete);
            if (driver.LicenseExpiry < DateOnly.FromDateTime(DateTime.Today))
                return (null, LicenseAlreadyExpired);
            if (PhotoStatusOf(await documents.GetCurrentAsync(driverId), out _) != PhotoAccepted)
                return (null, PhotosNotAccepted);
        }

        var actor = await ActorLookup.FindAsync(db, dispatcherId);
        driver.VerificationStatus = verified ? DriverVerificationStatuses.Verified : DriverVerificationStatuses.Rejected;
        driver.VerificationNote = verified ? null : text;
        driver.VerifiedByUserId = actor?.UserId;
        driver.VerifiedByName = actor?.Name;
        driver.VerifiedAt = DateTime.UtcNow;
        if (verified)
            UserNotifications.Add(db, driverId, "Bạn đã được xác minh",
                "Bằng lái của bạn đã được xác nhận. Bạn đã có thể nhận chuyến.");
        else
            UserNotifications.Add(db, driverId, "Xác minh bằng lái không thành công",
                $"Lý do: {text}. Vui lòng cập nhật lại thông tin bằng lái.");
        await db.SaveChangesAsync();
        return (await ToResponseAsync(driver), null);
    }

    public static bool InfoComplete(Driver d)
        => !string.IsNullOrWhiteSpace(d.LicenseNumber)
            && LicenseClasses.Find(d.LicenseClass) is { Professional: true, MaxSeats: > 0 }
            && d.LicenseExpiry > DateOnly.MinValue;

    /// <summary>Step-1 state of the four required photos, looking only at the newest of each side.</summary>
    public static string PhotoStatusOf(IReadOnlyList<UserDocumentResponse> current, out List<string> missing)
        => UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Driver), out missing);

    private async Task<DriverVerificationResponse> ToResponseAsync(Driver d)
    {
        var current = await documents.GetCurrentAsync(d.DriverId);
        var photoStatus = PhotoStatusOf(current, out var missing);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var hasExpiry = d.LicenseExpiry > DateOnly.MinValue;
        return new DriverVerificationResponse(
            d.DriverId, d.User.FullName, d.User.Email, d.User.Phone,
            string.IsNullOrWhiteSpace(d.LicenseNumber) ? null : d.LicenseNumber,
            d.LicenseClass, d.LicenseClass is null ? null : LicenseClasses.Label(d.LicenseClass),
            d.LicenseIssueDate, hasExpiry ? d.LicenseExpiry : null, d.IdNumber,
            d.VerificationStatus, photoStatus, missing, current,
            d.VerifiedByName, d.VerifiedAt, d.VerificationNote,
            hasExpiry && d.LicenseExpiry >= today && d.LicenseExpiry < today.AddDays(30),
            hasExpiry && d.LicenseExpiry < today,
            InfoComplete(d), d.IsActive);
    }
}
