using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Documents;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Where document photos are kept: a private folder, never under the public web root.</summary>
public sealed class DocumentStorage(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>Absolute path for a stored relative path; refuses anything that escapes the root.</summary>
    public string? Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}

/// <summary>
/// Upload / view / review of licence and ID-card photos (plan1.md Phần F). Only JPG or PNG up to 5 MB,
/// checked by the file's first bytes rather than its name. Viewing is limited to the owner, dispatchers
/// and admins. The photos never replace checking the original document in person.
/// </summary>
public class UserDocumentService(CarRentalDbContext db, DocumentStorage storage)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string FileRequired = "Vui lòng chọn ảnh.";
    public const string FileTooLarge = "Ảnh tối đa 5 MB.";
    public const string NotAnImage = "Chỉ nhận ảnh JPG hoặc PNG.";
    public const string InvalidType = "Loại giấy tờ không hợp lệ.";
    public const string NotFound = "Không tìm thấy ảnh giấy tờ.";
    public const string AlreadyReviewed = "Ảnh này đã được duyệt.";
    public const string ReasonRequired = "Vui lòng ghi lý do từ chối.";
    public const string ReasonTooLong = "Lý do tối đa 500 ký tự.";

    /// <summary>Photos a role must send: drivers both sides of licence and ID card; self-drive customers the licence.</summary>
    public static IReadOnlyList<string> RequiredFor(string role) => role switch
    {
        RoleNames.Driver => [UserDocumentTypes.LicenseFront, UserDocumentTypes.LicenseBack,
            UserDocumentTypes.IdCardFront, UserDocumentTypes.IdCardBack],
        RoleNames.Customer => [UserDocumentTypes.LicenseFront, UserDocumentTypes.LicenseBack],
        _ => []
    };

    public async Task<(UserDocumentResponse? Document, string? Error)> UploadAsync(
        int userId, string? documentType, Stream? content, long length)
    {
        if (!UserDocumentTypes.TryResolve(documentType, out var type))
            return (null, InvalidType);
        if (content is null || length <= 0)
            return (null, FileRequired);
        if (length > MaxBytes)
            return (null, FileTooLarge);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        if (buffer.Length == 0)
            return (null, FileRequired);
        if (buffer.Length > MaxBytes)
            return (null, FileTooLarge);
        var bytes = buffer.ToArray();
        var kind = DetectImage(bytes);
        if (kind is null)
            return (null, NotAnImage);

        var relative = Path.Combine(userId.ToString(), $"{Guid.NewGuid():N}.{kind.Value.Extension}");
        var full = storage.Resolve(relative)!;
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes);

        var doc = new UserDocument
        {
            UserId = userId,
            DocumentType = type,
            FilePath = relative.Replace('\\', '/'),
            ContentType = kind.Value.ContentType,
            SizeBytes = bytes.LongLength,
            Status = UserDocumentStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.UserDocuments.Add(doc);
        await db.SaveChangesAsync();
        return (await ToResponseAsync(doc), null);
    }

    /// <summary>The newest photo of each type for one user (what counts now).</summary>
    public async Task<List<UserDocumentResponse>> GetCurrentAsync(int userId)
    {
        var rows = await db.UserDocuments.AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UserDocumentId)
            .ToListAsync();
        return rows.GroupBy(d => d.DocumentType).Select(g => ToResponse(g.First())).ToList();
    }

    /// <summary>Staff list: newest first, optionally one user's or one status.</summary>
    public async Task<List<UserDocumentResponse>> ListAsync(int? userId, string? status)
    {
        var query = db.UserDocuments.AsNoTracking().Include(d => d.User).AsQueryable();
        if (userId is int id)
            query = query.Where(d => d.UserId == id);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status.Trim());
        var rows = await query.OrderByDescending(d => d.UserDocumentId).Take(500).ToListAsync();
        return rows.Select(ToResponse).ToList();
    }

    /// <summary>Types still missing for a user: no photo yet, or the newest one was rejected.</summary>
    public async Task<List<string>> GetMissingAsync(int userId, IReadOnlyList<string> required)
    {
        var current = await GetCurrentAsync(userId);
        return required
            .Where(t => !current.Any(d => d.DocumentType == t && d.Status != UserDocumentStatuses.Rejected))
            .ToList();
    }

    /// <summary>The file, if the requester may see it (owner, dispatcher or admin).</summary>
    public async Task<(string? FullPath, string? ContentType, int Status)> GetFileAsync(
        int documentId, int requesterId, string? requesterRole)
    {
        var doc = await db.UserDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.UserDocumentId == documentId);
        if (doc is null)
            return (null, null, StatusCodes.Status404NotFound);
        var staff = requesterRole is RoleNames.Dispatcher or RoleNames.Admin;
        if (!staff && doc.UserId != requesterId)
            return (null, null, StatusCodes.Status403Forbidden);
        var full = storage.Resolve(doc.FilePath);
        if (full is null || !File.Exists(full))
            return (null, null, StatusCodes.Status404NotFound);
        return (full, doc.ContentType, StatusCodes.Status200OK);
    }

    public async Task<(UserDocumentResponse? Document, string? Error, int Status)> ReviewAsync(
        int documentId, int reviewerId, bool approve, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!approve && text is null)
            return (null, ReasonRequired, StatusCodes.Status400BadRequest);
        if (text is { Length: > 500 })
            return (null, ReasonTooLong, StatusCodes.Status400BadRequest);

        var doc = await db.UserDocuments.Include(d => d.User).FirstOrDefaultAsync(d => d.UserDocumentId == documentId);
        if (doc is null)
            return (null, NotFound, StatusCodes.Status404NotFound);
        if (doc.Status != UserDocumentStatuses.Pending)
            return (null, AlreadyReviewed, StatusCodes.Status400BadRequest);

        var actor = await ActorLookup.FindAsync(db, reviewerId);
        doc.Status = approve ? UserDocumentStatuses.Approved : UserDocumentStatuses.Rejected;
        doc.RejectReason = approve ? null : text;
        doc.ReviewedByUserId = actor?.UserId;
        doc.ReviewedByName = actor?.Name;
        doc.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (ToResponse(doc), null, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Review state of the required photos, newest of each side only:
    /// Missing (a side not sent) / Rejected (one rejected) / Pending / Accepted (all approved).
    /// </summary>
    public static string PhotoStatus(IReadOnlyList<UserDocumentResponse> current, IReadOnlyList<string> required, out List<string> missing)
    {
        missing = required.Where(t => current.All(d => d.DocumentType != t)).ToList();
        var relevant = current.Where(d => required.Contains(d.DocumentType)).ToList();
        if (relevant.Any(d => d.Status == UserDocumentStatuses.Rejected))
            return "Rejected";
        if (missing.Count > 0)
            return "Missing";
        return relevant.All(d => d.Status == UserDocumentStatuses.Approved) ? "Accepted" : "Pending";
    }

    /// <summary>JPEG starts FF D8 FF; PNG starts 89 50 4E 47 0D 0A 1A 0A. Anything else is refused.</summary>
    public static (string ContentType, string Extension)? DetectImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ("image/jpeg", "jpg");
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.Length >= png.Length && bytes[..png.Length].SequenceEqual(png))
            return ("image/png", "png");
        return null;
    }

    private async Task<UserDocumentResponse> ToResponseAsync(UserDocument doc)
    {
        await db.Entry(doc).Reference(d => d.User).LoadAsync();
        return ToResponse(doc);
    }

    private static UserDocumentResponse ToResponse(UserDocument d) => new(
        d.UserDocumentId, d.UserId, d.User?.FullName, d.DocumentType, d.ContentType, d.SizeBytes, d.Status,
        d.ReviewedByName, d.ReviewedAt, d.RejectReason, d.CreatedAt);
}
