using System.Text.Json;
using CustomerWeb.Models;

namespace CustomerWeb.Services;

public class AuthSession(IHttpContextAccessor httpContextAccessor)
{
    public const string CookieName = "CarRentalAuth";

    public AuthResponse? GetAuth()
    {
        var context = httpContextAccessor.HttpContext;
        if (context?.Request.Cookies.TryGetValue(CookieName, out var json) != true || string.IsNullOrEmpty(json))
            return null;

        try
        {
            var auth = JsonSerializer.Deserialize<AuthResponse>(json);
            return auth?.ExpiresAt > DateTime.UtcNow ? auth : null;
        }
        catch
        {
            return null;
        }
    }

    public void SetAuth(AuthResponse auth)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return;

        var json = JsonSerializer.Serialize(auth);
        context.Response.Cookies.Append(CookieName, json, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Expires = auth.ExpiresAt
        });
    }

    public void Clear()
    {
        var context = httpContextAccessor.HttpContext;
        context?.Response.Cookies.Delete(CookieName);
    }

    public string? Token => GetAuth()?.Token;
    public bool IsLoggedIn => GetAuth() is not null;
    public string? Role => GetAuth()?.Role;
    public string? FullName => GetAuth()?.FullName;
}
