using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortalWeb.Services;

namespace PortalWeb.Pages.Documents;

/// <summary>
/// Shows a licence / ID-card photo (plan1.md Phần F). The browser has no API token, so PortalWeb fetches the
/// file with the signed-in user's token; the Backend decides who may see it (owner, dispatcher, admin).
/// Never cached, since these are personal documents.
/// </summary>
public class FileModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (string.IsNullOrEmpty(auth.Token))
            return RedirectToPage("/Account/Login");

        var (bytes, contentType, status) = await api.GetDocumentFileAsync(id);
        if (bytes is null)
            return status is 401 or 403 ? StatusCode(StatusCodes.Status403Forbidden) : NotFound();

        Response.Headers.CacheControl = "no-store, private";
        return File(bytes, contentType ?? "application/octet-stream");
    }
}
