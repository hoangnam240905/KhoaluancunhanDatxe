using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PortalWeb.Pages;

/// <summary>
/// Public credits for the vehicle photos (plan1.md 5.2: CC BY / CC BY-SA require attribution).
/// The single source is wwwroot/images/vehicles/CREDITS.md; this page shows its table.
/// </summary>
public partial class CreditsModel(IWebHostEnvironment env) : PageModel
{
    public sealed record Credit(string File, string Title, string SourceUrl, string Author, string License, string LicenseUrl);

    public IReadOnlyList<Credit> Credits { get; private set; } = [];

    public void OnGet()
    {
        var path = Path.Combine(env.WebRootPath, "images", "vehicles", "CREDITS.md");
        if (!System.IO.File.Exists(path)) return;
        Credits = System.IO.File.ReadLines(path)
            .Select(line => Row().Match(line))
            .Where(m => m.Success)
            .Select(m => new Credit(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value,
                m.Groups[4].Value.Trim(), m.Groups[5].Value, m.Groups[6].Value))
            .ToList();
    }

    // | `file.jpg` | [title](url) | author | [licence](url) | — Commons URLs may contain "(" and ")".
    [GeneratedRegex(@"^\|\s*`([^`]+)`\s*\|\s*\[([^\]]+)\]\((\S+)\)\s*\|([^|]+)\|\s*\[([^\]]+)\]\((\S+)\)\s*\|")]
    private static partial Regex Row();
}
