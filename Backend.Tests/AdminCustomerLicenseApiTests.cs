using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.DTOs.Auth;
using Backend.DTOs.Customers;
using Xunit;

namespace Backend.Tests;

/// <summary>Admin reviews self-drive customer licences like a dispatcher (same service, admin routes).</summary>
public class AdminCustomerLicenseApiTests
{
    [Fact]
    public async Task Admin_lists_reads_and_reviews_customer_licences_dispatcher_cannot_use_admin_routes()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var admin = await LoginAsync(client, "admin@carrental.vn");
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");

        var approved = await Get<List<CustomerLicenseResponse>>(client, admin, "/api/admin/customers/licenses?status=Approved");
        Assert.Contains(approved, c => c.CustomerId == 3);
        var one = await Get<CustomerLicenseResponse>(client, admin, "/api/admin/customers/3/license");
        Assert.Equal("Approved", one.LicenseStatus);

        var review = new HttpRequestMessage(HttpMethod.Post, "/api/admin/customers/3/license/review")
        {
            Content = JsonContent.Create(new ReviewCustomerLicenseRequest(false, "Ảnh mặt sau bị mờ"))
        };
        review.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        var reviewed = await client.SendAsync(review);
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        var result = await reviewed.Content.ReadFromJsonAsync<CustomerLicenseResponse>();
        Assert.Equal("Rejected", result!.LicenseStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.ReviewedByName));

        var forbidden = new HttpRequestMessage(HttpMethod.Get, "/api/admin/customers/licenses");
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dispatcher);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(forbidden)).StatusCode);
    }

    private static async Task<T> Get<T>(HttpClient client, string token, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
