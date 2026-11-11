using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Documents;
using Backend.DTOs.Drivers;
using Backend.DTOs.Licences;
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
        if (driver is null)
            return null;
        await LicenceBook.LoadAsync(db, driver);
        return await ToResponseAsync(driver);
    }

    /// <summary>Dispatcher list; status "Pending" = a licence still to verify (the default), "Verified", "Rejected", or "All".</summary>
    public async Task<List<DriverVerificationResponse>> ListAsync(string? status)
    {
        var rows = await db.Drivers.AsNoTracking().Include(d => d.User).Where(d => d.IsActive).OrderBy(d => d.DriverId).ToListAsync();
        await LicenceBook.LoadAsync(db, rows);
        var wanted = string.IsNullOrWhiteSpace(status) ? DriverVerificationStatuses.Pending : status.Trim();
        var all = string.Equals(wanted, "All", StringComparison.OrdinalIgnoreCase);
        var result = new List<DriverVerificationResponse>();
        foreach (var driver in rows)
        {
            var match = all || driver.VerificationStatus == wanted
                || (wanted == DriverVerificationStatuses.Pending
                    && LicenceBook.Of(driver).Any(l => l.Status == PersonLicenseStatuses.Pending));
            if (match)
                result.Add(await ToResponseAsync(driver));
        }
        return result;
    }

    /// <summary>Declare the first licence or correct the primary one (older single-licence screens).</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> UpdateLicenseAsync(int driverId, UpdateDriverLicenseRequest request)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        return await SaveAsync(driver, rows, rows.FirstOrDefault(), request);
    }

    /// <summary>Phase 9: add another licence (e.g. D1 next to B). Other licences keep their status.</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> AddLicenceAsync(int driverId, UpdateDriverLicenseRequest request)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        return await SaveAsync(driver, rows, null, request);
    }

    /// <summary>Phase 9: correct one licence; only that licence goes back to "Chờ xác minh".</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> UpdateLicenceAsync(int driverId, int licenseId, UpdateDriverLicenseRequest request)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        var licence = rows.FirstOrDefault(l => l.PersonLicenseId == licenseId);
        return licence is null ? (null, LicenceBook.NotFound) : await SaveAsync(driver, rows, licence, request);
    }

    private async Task<(DriverVerificationResponse? Data, string? Error)> SaveAsync(
        Driver driver, List<PersonLicense> rows, PersonLicense? licence, UpdateDriverLicenseRequest request)
    {
        var number = LicenseNumberRules.Normalize(request.LicenseNumber);
        if (number is null)
            return (null, LicenseNumberRequired);
        if (!LicenseNumberRules.IsValid(number))
            return (null, LicenseNumberRules.Invalid);
        if (number != licence?.Number
            && await LicenceBook.NumberProblemAsync(db, number, LicenceOwnerKinds.Driver, driver.DriverId, licence?.PersonLicenseId) is string numberError)
            return (null, numberError);
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

        if (licence is null)
        {
            licence = new PersonLicense
            {
                OwnerKind = LicenceOwnerKinds.Driver, OwnerId = driver.DriverId,
                Status = PersonLicenseStatuses.Pending, CreatedAt = DateTime.UtcNow
            };
            db.PersonLicenses.Add(licence);
            rows.Add(licence);
        }
        var changed = licence.Number != number || licence.Class != cls.Code
            || licence.Expiry != expiry || licence.IssueDate != request.LicenseIssueDate
            || (idNumber is not null && driver.IdNumber != idNumber);
        licence.Number = number;
        licence.Class = cls.Code;
        licence.Expiry = expiry;
        licence.IssueDate = request.LicenseIssueDate;
        if (idNumber is not null)
            driver.IdNumber = idNumber;
        if (changed && licence.Status != PersonLicenseStatuses.Pending)
        {
            // A new or corrected licence must be checked again before it counts for an assignment.
            licence.Status = PersonLicenseStatuses.Pending;
            licence.ReviewedByUserId = null;
            licence.ReviewedByName = null;
            licence.ReviewedAt = null;
        }
        LicenceBook.Mirror(driver, rows);
        driver.User.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await NotifyDispatchersIfReadyAsync(driver.DriverId);
        return (await ToResponseAsync(driver), null);
    }

    /// <summary>DP-1: once a pending licence is declared and every photo sent, tell the dispatchers.</summary>
    public async Task NotifyDispatchersIfReadyAsync(int driverId)
    {
        var driver = await db.Drivers.AsNoTracking().Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return;
        await LicenceBook.LoadAsync(db, driver);
        var licences = LicenceBook.Of(driver);
        var ready = false;
        foreach (var l in licences.Where(l => l.Status == PersonLicenseStatuses.Pending && InfoComplete(l)))
            if (PhotoStatusOf(await PhotosOfAsync(driverId, licences, l), out _) == PhotoPending)
                ready = true;
        if (!ready)
            return;

        var message = $"Tài xế {driver.User.FullName} vừa gửi ảnh bằng lái, cần xác minh.";
        var since = DateTime.UtcNow.AddMinutes(-10);
        if (await db.Notifications.AnyAsync(n => n.Message == message && n.CreatedAt >= since))
            return; // several photos sent one after another → one notification
        await UserNotifications.AddToDispatchersAsync(db, "Hồ sơ bằng lái cần duyệt", message);
        await db.SaveChangesAsync();
    }

    /// <summary>Step 1 for the first licence still waiting (else the primary) — older single-licence pages.</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> ReviewPhotosAsync(
        int driverId, int dispatcherId, bool accept, string? reason)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        var target = rows.FirstOrDefault(l => l.Status == PersonLicenseStatuses.Pending) ?? rows.FirstOrDefault();
        return await ReviewPhotosCoreAsync(driver, rows, target, dispatcherId, accept, reason);
    }

    /// <summary>Phase 9: step 1 for one licence.</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> ReviewLicencePhotosAsync(
        int driverId, int licenseId, int dispatcherId, bool accept, string? reason)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        var target = rows.FirstOrDefault(l => l.PersonLicenseId == licenseId);
        return target is null
            ? (null, LicenceBook.NotFound)
            : await ReviewPhotosCoreAsync(driver, rows, target, dispatcherId, accept, reason);
    }

    /// <summary>Step 1 (online): accept every pending photo of the licence (TX-1) or reject them with a reason (TX-2).</summary>
    private async Task<(DriverVerificationResponse? Data, string? Error)> ReviewPhotosCoreAsync(
        Driver driver, List<PersonLicense> rows, PersonLicense? licence, int dispatcherId, bool accept, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!accept && text is null)
            return (null, ReasonRequired);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong);
        if (licence is null)
            return (null, InfoIncomplete);
        if (licence.Status == PersonLicenseStatuses.Approved)
            return (null, AlreadyVerified);

        var current = await PhotosOfAsync(driver.DriverId, rows, licence);
        if (accept)
        {
            if (!InfoComplete(licence))
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

        var label = rows.Count > 1 ? $" hạng {LicenseClasses.Label(licence.Class)}" : "";
        if (accept)
            UserNotifications.Add(db, driver.DriverId, $"Bằng lái{label} đã được chấp nhận",
                $"Vui lòng lên văn phòng trong vòng {OfficeVisitDays} ngày tới để xác nhận bằng gốc. Mang theo bằng lái và CCCD. Sau khi xác minh xong, bạn sẽ được phân công chuyến.");
        else
            UserNotifications.Add(db, driver.DriverId, $"Ảnh bằng lái{label} chưa đạt",
                $"Lý do: {text}. Vui lòng chụp lại ảnh rõ hơn và gửi lại.");
        await db.SaveChangesAsync();
        return (await ToResponseAsync(driver), null);
    }

    /// <summary>Step 2 for the licence whose photos were accepted (else the first waiting, else the primary).</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> VerifyAsync(
        int driverId, int dispatcherId, bool verified, string? reason)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        PersonLicense? target = null;
        foreach (var l in rows.Where(l => l.Status == PersonLicenseStatuses.Pending))
            if (PhotoStatusOf(await PhotosOfAsync(driverId, rows, l), out _) == PhotoAccepted)
            {
                target = l;
                break;
            }
        target ??= rows.FirstOrDefault(l => l.Status == PersonLicenseStatuses.Pending) ?? rows.FirstOrDefault();
        return await VerifyCoreAsync(driver, rows, target, dispatcherId, verified, reason);
    }

    /// <summary>Phase 9: step 2 for one licence.</summary>
    public async Task<(DriverVerificationResponse? Data, string? Error)> VerifyLicenceAsync(
        int driverId, int licenseId, int dispatcherId, bool verified, string? reason)
    {
        var driver = await db.Drivers.Include(d => d.User).FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver is null)
            return (null, NotFound);
        var rows = await LicenceBook.EnsureRowsAsync(db, driver);
        var target = rows.FirstOrDefault(l => l.PersonLicenseId == licenseId);
        return target is null
            ? (null, LicenceBook.NotFound)
            : await VerifyCoreAsync(driver, rows, target, dispatcherId, verified, reason);
    }

    /// <summary>Step 2 (at the office): the original licence matches → Approved (TX-3); otherwise Rejected with a reason (TX-4).</summary>
    private async Task<(DriverVerificationResponse? Data, string? Error)> VerifyCoreAsync(
        Driver driver, List<PersonLicense> rows, PersonLicense? licence, int dispatcherId, bool verified, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!verified && text is null)
            return (null, ReasonRequired);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong);
        if (licence is null)
            return (null, InfoIncomplete);

        if (verified)
        {
            if (licence.Status == PersonLicenseStatuses.Approved)
                return (null, AlreadyVerified);
            if (!InfoComplete(licence))
                return (null, InfoIncomplete);
            if (licence.Expiry < DateOnly.FromDateTime(DateTime.Today))
                return (null, LicenseAlreadyExpired);
            if (PhotoStatusOf(await PhotosOfAsync(driver.DriverId, rows, licence), out _) != PhotoAccepted)
                return (null, PhotosNotAccepted);
        }

        var actor = await ActorLookup.FindAsync(db, dispatcherId);
        licence.Status = verified ? PersonLicenseStatuses.Approved : PersonLicenseStatuses.Rejected;
        licence.ReviewNote = verified ? null : text;
        licence.ReviewedByUserId = actor?.UserId;
        licence.ReviewedByName = actor?.Name;
        licence.ReviewedAt = DateTime.UtcNow;
        LicenceBook.Mirror(driver, rows);
        var label = rows.Count > 1 ? $" hạng {LicenseClasses.Label(licence.Class)}" : "";
        if (verified)
            UserNotifications.Add(db, driver.DriverId, rows.Count > 1 ? $"Bằng lái{label} đã được xác minh" : "Bạn đã được xác minh",
                $"Bằng lái{label} của bạn đã được xác nhận. Bạn đã có thể nhận chuyến.");
        else
            UserNotifications.Add(db, driver.DriverId, $"Xác minh bằng lái{label} không thành công",
                $"Lý do: {text}. Vui lòng cập nhật lại thông tin bằng lái.");
        await db.SaveChangesAsync();
        return (await ToResponseAsync(driver), null);
    }

    public static bool InfoComplete(Driver d)
        => !string.IsNullOrWhiteSpace(d.LicenseNumber)
            && LicenseClasses.Find(d.LicenseClass) is { Professional: true, MaxSeats: > 0 }
            && d.LicenseExpiry > DateOnly.MinValue;

    public static bool InfoComplete(PersonLicense l)
        => !string.IsNullOrWhiteSpace(l.Number)
            && LicenseClasses.Find(l.Class) is { Professional: true, MaxSeats: > 0 }
            && l.Expiry > DateOnly.MinValue;

    /// <summary>Step-1 state of the four required photos, looking only at the newest of each side.</summary>
    public static string PhotoStatusOf(IReadOnlyList<UserDocumentResponse> current, out List<string> missing)
        => UserDocumentService.PhotoStatus(current, UserDocumentService.RequiredFor(RoleNames.Driver), out missing);

    /// <summary>The current photos of one licence plus the ID card (untagged licence photos belong to the primary licence).</summary>
    private Task<List<UserDocumentResponse>> PhotosOfAsync(int driverId, IReadOnlyList<PersonLicense> rows, PersonLicense licence)
        => documents.GetCurrentAsync(driverId, licence.PersonLicenseId == 0 ? null : licence.PersonLicenseId, LicenceBook.IsPrimary(rows, licence));

    private async Task<DriverVerificationResponse> ToResponseAsync(Driver d)
    {
        var licences = LicenceBook.Of(d);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = new List<PersonLicenseResponse>();
        foreach (var l in licences)
        {
            var docs = await PhotosOfAsync(d.DriverId, licences, l);
            var ps = PhotoStatusOf(docs, out var miss);
            items.Add(new PersonLicenseResponse(l.PersonLicenseId, l.Number, l.Class, LicenseClasses.Label(l.Class), l.IssueDate,
                l.Expiry, l.Status, ps, miss, docs, l.ReviewedByName, l.ReviewedAt, l.ReviewNote, LicenceBook.IsPrimary(licences, l),
                l.Expiry >= today && l.Expiry < today.AddDays(30), l.Expiry < today));
        }
        var current = items.FirstOrDefault()?.Documents.ToList() ?? await documents.GetCurrentAsync(d.DriverId);
        var photoStatus = PhotoStatusOf(current, out var missing);
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
            InfoComplete(d), d.IsActive, items);
    }
}
