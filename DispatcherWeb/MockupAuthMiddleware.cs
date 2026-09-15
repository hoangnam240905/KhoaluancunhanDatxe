using DispatcherWeb.Services;

namespace DispatcherWeb;

/// <summary>
/// Server-side gate for /Mockup/* — requires Dispatcher cookie session.
/// </summary>
public static class MockupAuthMiddleware
{
    public static IApplicationBuilder UseMockupAuthGate(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/Mockup", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var auth = context.RequestServices.GetRequiredService<AuthSession>();
            if (!auth.IsLoggedIn)
            {
                var returnUrl = path + context.Request.QueryString.Value;
                context.Response.Redirect("/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
                return;
            }

            if (!string.Equals(auth.Role, "Dispatcher", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/Account/Forbidden");
                return;
            }

            await next();
        });
    }
}
