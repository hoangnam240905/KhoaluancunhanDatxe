using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Services;

/// Local CustomerWeb login navigation. Rejects open redirects; never sends users to PortalWeb.
public static class CustomerLoginRedirect
{
    public static IActionResult ToLogin(PageModel page)
    {
        var returnUrl = $"{page.Request.PathBase}{page.Request.Path}{page.Request.QueryString}";
        return page.RedirectToPage("/Account/Login", new { returnUrl });
    }

    public static IActionResult AfterLogin(PageModel page, string? returnUrl)
    {
        if (IsSafeLocalReturnUrl(page, returnUrl))
            return page.LocalRedirect(returnUrl!);

        return page.RedirectToPage("/Index");
    }

    public static bool IsSafeLocalReturnUrl(PageModel page, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (!page.Url.IsLocalUrl(returnUrl))
            return false;

        var path = returnUrl.Trim();
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];

        if (path.StartsWith("~/", StringComparison.Ordinal))
            path = path[1..];

        return !path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase);
    }
}
