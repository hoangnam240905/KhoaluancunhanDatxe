using System.Text.Json;
using DispatcherWeb.Models;

namespace DispatcherWeb.Services;

public class AuthSession(IHttpContextAccessor httpContextAccessor)
{
    public const string CookieName = "CarRentalDispatcherAuth";

    public AuthResponse? GetAuth()
    {
        var context = httpContextAccessor.HttpContext;
        if (context?.Request.Cookies.TryGetValue(CookieName, out var json) != true || string.IsNullOrEmpty(json)) return null;
        try
        {
            var auth = JsonSerializer.Deserialize<AuthResponse>(json);
            if (auth is null) return null;
            if (auth.ExpiresAt > DateTime.UtcNow) return auth;
            // Expired JWT cookie — treat as logged out (session expiration).
            context.Response.Cookies.Delete(CookieName);
            return null;
        }
        catch { return null; }
    }

    public void SetAuth(AuthResponse auth)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return;
        context.Response.Cookies.Append(CookieName, JsonSerializer.Serialize(auth), new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = false, Expires = auth.ExpiresAt
        });
    }

    public void Clear() => httpContextAccessor.HttpContext?.Response.Cookies.Delete(CookieName);
    public string? Token => GetAuth()?.Token;
    public bool IsLoggedIn => GetAuth() is not null;
    public string? Role => GetAuth()?.Role;
    public string? FullName => GetAuth()?.FullName;
    public string? Email => GetAuth()?.Email;
}
