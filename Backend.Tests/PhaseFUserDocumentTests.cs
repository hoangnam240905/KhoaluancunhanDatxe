using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Documents;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Phần F — licence / ID-card photo upload (F-T01 … F-T04).</summary>
public sealed class PhaseFUserDocumentTests : IDisposable
{
    private const int DispatcherId = 2;
    private const int CustomerId = 3;
    private const int DriverId = 6;
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crs-docs-unit-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { /* temp folder */ }
    }

    private static byte[] Jpeg(int size)
    {
        var bytes = new byte[size];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; bytes[3] = 0xE0;
        return bytes;
    }

    private static byte[] Png(int size)
    {
        var bytes = new byte[size];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    private UserDocumentService Service(IsolatedCarRentalDb iso) => new(iso.Db, new DocumentStorage(root));

    private static Task<(UserDocumentResponse? Document, string? Error)> Upload(
        UserDocumentService s, int userId, string type, byte[] bytes)
        => s.UploadAsync(userId, type, new MemoryStream(bytes), bytes.Length);

    [Fact]
    public async Task F01_jpg_3mb_is_stored_privately_and_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Service(iso);

        var (doc, error) = await Upload(s, DriverId, UserDocumentTypes.LicenseFront, Jpeg(3 * 1024 * 1024));

        Assert.True(doc is not null, error);
        Assert.Equal(UserDocumentStatuses.Pending, doc!.Status);
        Assert.Equal("image/jpeg", doc.ContentType);
        var stored = iso.Db.UserDocuments.Single();
        Assert.DoesNotContain("wwwroot", stored.FilePath);
        Assert.True(File.Exists(Path.Combine(root, stored.FilePath)));
        Assert.Equal("Võ Thị Lái", doc.UserName);

        var (png, _) = await Upload(s, DriverId, UserDocumentTypes.IdCardFront, Png(2048));
        Assert.Equal("image/png", png!.ContentType);
    }

    [Fact]
    public async Task F02_non_image_too_large_renamed_or_unknown_type_is_refused()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Service(iso);

        // A PDF / text file renamed to .jpg: the name is never looked at, only the first bytes.
        var fake = "%PDF-1.7 not an image"u8.ToArray();
        Assert.Equal(UserDocumentService.NotAnImage, (await Upload(s, DriverId, UserDocumentTypes.LicenseFront, fake)).Error);
        Assert.Equal(UserDocumentService.FileTooLarge,
            (await Upload(s, DriverId, UserDocumentTypes.LicenseFront, Jpeg((int)UserDocumentService.MaxBytes + 1))).Error);
        Assert.Equal(UserDocumentService.FileRequired, (await Upload(s, DriverId, UserDocumentTypes.LicenseFront, [])).Error);
        Assert.Equal(UserDocumentService.InvalidType, (await Upload(s, DriverId, "Passport", Jpeg(100))).Error);
        Assert.Empty(iso.Db.UserDocuments);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task F03_review_records_reviewer_reject_needs_reason_and_owner_can_send_again()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Service(iso);
        var (doc, _) = await Upload(s, DriverId, UserDocumentTypes.LicenseFront, Jpeg(500));

        Assert.Equal(UserDocumentService.ReasonRequired, (await s.ReviewAsync(doc!.UserDocumentId, DispatcherId, false, " ")).Error);
        var (rejected, error, _) = await s.ReviewAsync(doc.UserDocumentId, DispatcherId, false, "Ảnh mờ, không đọc được số bằng");
        Assert.True(rejected is not null, error);
        Assert.Equal(UserDocumentStatuses.Rejected, rejected!.Status);
        Assert.Equal("Trần Thị Điều Phối", rejected.ReviewedByName);
        Assert.Equal("Ảnh mờ, không đọc được số bằng", rejected.RejectReason);
        Assert.Equal(UserDocumentService.AlreadyReviewed, (await s.ReviewAsync(doc.UserDocumentId, DispatcherId, true, null)).Error);

        var required = UserDocumentService.RequiredFor(RoleNames.Driver);
        Assert.Contains(UserDocumentTypes.LicenseFront, await s.GetMissingAsync(DriverId, required));

        var (again, _) = await Upload(s, DriverId, UserDocumentTypes.LicenseFront, Jpeg(600));
        var current = Assert.Single(await s.GetCurrentAsync(DriverId));
        Assert.Equal(again!.UserDocumentId, current.UserDocumentId);
        Assert.Equal(UserDocumentStatuses.Pending, current.Status);
        Assert.DoesNotContain(UserDocumentTypes.LicenseFront, await s.GetMissingAsync(DriverId, required));

        var (approved, _, _) = await s.ReviewAsync(again.UserDocumentId, DispatcherId, true, null);
        Assert.Equal(UserDocumentStatuses.Approved, approved!.Status);
        Assert.Null(approved.RejectReason);
        Assert.Equal(2, UserDocumentService.RequiredFor(RoleNames.Customer).Count);
    }

    [Fact]
    public async Task F04_only_owner_dispatcher_and_admin_can_view_the_file()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var driver = await LoginAsync(client, "driver2@carrental.vn");
        var otherDriver = await LoginAsync(client, "driver1@carrental.vn");
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        var admin = await LoginAsync(client, "admin@carrental.vn");

        var form = new MultipartFormDataContent
        {
            { new StringContent(UserDocumentTypes.LicenseFront), "type" },
            { new ByteArrayContent(Jpeg(1024)) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "file", "bang-lai.jpg" }
        };
        var uploaded = await client.SendAsync(Authed(HttpMethod.Post, "/api/documents", driver, form));
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var doc = await uploaded.Content.ReadFromJsonAsync<UserDocumentResponse>();
        Assert.StartsWith(DriverId + Path.DirectorySeparatorChar.ToString(),
            Path.GetRelativePath(factory.DocumentRoot, Directory.EnumerateFiles(factory.DocumentRoot, "*", SearchOption.AllDirectories).Single()));

        var url = $"/api/documents/{doc!.UserDocumentId}/file";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get, url, otherDriver))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Get, url, customer))).StatusCode);
        foreach (var token in new[] { driver, dispatcher, admin })
        {
            var file = await client.SendAsync(Authed(HttpMethod.Get, url, token));
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.Equal("image/jpeg", file.Content.Headers.ContentType!.MediaType);
            Assert.Equal(1024, (await file.Content.ReadAsByteArrayAsync()).Length);
        }

        // Review is staff-only; the owner sees the result in /me.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Post,
            $"/api/documents/{doc.UserDocumentId}/review", driver, JsonContent.Create(new ReviewDocumentRequest(true, null))))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authed(HttpMethod.Post,
            $"/api/documents/{doc.UserDocumentId}/review", dispatcher, JsonContent.Create(new ReviewDocumentRequest(true, null))))).StatusCode);
        var mine = await (await client.SendAsync(Authed(HttpMethod.Get, "/api/documents/me", driver)))
            .Content.ReadFromJsonAsync<List<UserDocumentResponse>>();
        Assert.Equal(UserDocumentStatuses.Approved, Assert.Single(mine!).Status);

        // Dispatchers cannot upload documents for themselves.
        var staffForm = new MultipartFormDataContent
        {
            { new StringContent(UserDocumentTypes.LicenseFront), "type" },
            { new ByteArrayContent(Jpeg(64)), "file", "x.jpg" }
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authed(HttpMethod.Post, "/api/documents", dispatcher, staffForm))).StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
