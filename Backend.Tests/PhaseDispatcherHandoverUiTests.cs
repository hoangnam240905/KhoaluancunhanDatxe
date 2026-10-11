using Xunit;

namespace Backend.Tests;

public class PhaseDispatcherHandoverUiTests
{
    [Fact]
    public void Portal_dispatcher_list_navigates_to_handover_page_and_does_not_post_api()
    {
        var indexView = Read("PortalWeb", "Pages", "Dispatcher", "Index.cshtml");
        var indexCode = Read("PortalWeb", "Pages", "Dispatcher", "Index.cshtml.cs");
        var handoverView = Read("PortalWeb", "Pages", "Dispatcher", "Handover.cshtml");
        var handoverCode = Read("PortalWeb", "Pages", "Dispatcher", "Handover.cshtml.cs");

        Assert.Contains("asp-page=\"/Dispatcher/Handover\"", indexView);
        Assert.Contains(">Giao xe</a>", indexView);
        Assert.DoesNotContain("asp-page-handler=\"Handover\"", indexView);
        Assert.DoesNotContain("HandoverBookingAsync", indexCode);
        Assert.Contains("RedirectToPage(\"/Dispatcher/Handover\"", indexCode);
        Assert.Contains("OnPostHandover", indexCode);
        Assert.Contains("OnGetAsync", handoverCode);
        Assert.DoesNotContain("HandoverBookingAsync", OnGetBody(handoverCode));
        Assert.Contains("asp-page=\"/Dispatcher/Handover\"", handoverView);
        Assert.Contains("Input.OdometerKm", handoverView);
        Assert.Contains("Input.FuelLevel", handoverView);
        Assert.Contains("Input.ExteriorCondition", handoverView);
        Assert.Contains("Input.TechnicalCondition", handoverView);
        Assert.Contains("Input.Notes", handoverView);
    }

    [Fact]
    public void DispatcherWeb_list_navigates_to_handover_page_and_does_not_post_api()
    {
        var indexView = Read("DispatcherWeb", "Pages", "Index.cshtml");
        var indexCode = Read("DispatcherWeb", "Pages", "Index.cshtml.cs");
        var handoverCode = Read("DispatcherWeb", "Pages", "Handover.cshtml.cs");

        Assert.Contains("asp-page=\"/Handover\"", indexView);
        Assert.Contains(">Giao xe</a>", indexView);
        Assert.DoesNotContain("asp-page-handler=\"Handover\"", indexView);
        Assert.DoesNotContain("HandoverBookingAsync", indexCode);
        Assert.Contains("RedirectToPage(\"/Handover\"", indexCode);
        Assert.DoesNotContain("HandoverBookingAsync", OnGetBody(handoverCode));
    }

    private static string OnGetBody(string code)
    {
        var start = code.IndexOf("OnGetAsync", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var post = code.IndexOf("OnPostAsync", start, StringComparison.Ordinal);
        Assert.True(post > start);
        return code[start..post];
    }

    private static string Read(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        string? root = null;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Backend", "Backend.csproj")))
            {
                root = dir;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        Assert.False(string.IsNullOrEmpty(root));
        return File.ReadAllText(Path.Combine(new[] { root! }.Concat(parts).ToArray()));
    }
}
