using Xunit;

namespace Backend.Tests;

public class WebBusyCalendarContractTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CustomerWeb", "wwwroot", "js", "catalog.js")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate CarRentalSystem repo root.");
    }

    [Theory]
    [InlineData("CustomerWeb")]
    [InlineData("PortalWeb")]
    public void Catalog_js_treats_empty_json_array_as_success_and_requests_visible_month(string web)
    {
        var js = File.ReadAllText(Path.Combine(RepoRoot(), web, "wwwroot", "js", "catalog.js"));
        Assert.Contains("Không có lịch bận trong khoảng thời gian này.", js);
        Assert.Contains("const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;", js);
        Assert.Contains("const from = new Date(year, month, 1, 0, 0, 0);", js);
        Assert.Contains("const to = new Date(year, month + 1, 1, 0, 0, 0);", js);
        Assert.Contains("return Array.isArray(data) ? data : null;", js);
        Assert.Contains("if (type.includes(\"html\")) return null;", js);
        Assert.Contains("if (!response.ok) return null;", js);
        Assert.Contains("let seq = 0;", js);
        Assert.DoesNotContain("if (!url || loading) return;", js);
        Assert.DoesNotContain("isoLocal", js);
    }

    [Theory]
    [InlineData("CustomerWeb")]
    [InlineData("PortalWeb")]
    public void Busy_periods_proxy_and_handler_return_json(string web)
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), web, "Program.cs"));
        Assert.Contains("MapGet(\"/catalog/busy-periods/{vehicleId:int}\"", program);
        Assert.Contains("Results.Json(data)", program);

        var handler = File.ReadAllText(Path.Combine(RepoRoot(), web, "Pages", "Catalog", "Details.cshtml.cs"));
        Assert.Contains("OnGetBusyPeriodsAsync", handler);
        Assert.Contains("return new JsonResult(data);", handler);
        Assert.Contains("return new JsonResult(new { message = \"Không tìm thấy xe.\" }) { StatusCode = 404 };", handler);
        Assert.DoesNotContain("return NotFound();", handler.Substring(handler.IndexOf("OnGetBusyPeriodsAsync", StringComparison.Ordinal)));

        var page = File.ReadAllText(Path.Combine(RepoRoot(), web, "Pages", "Catalog", "Details.cshtml"));
        Assert.Contains("data-cd-busy-url=\"/catalog/busy-periods/@v.VehicleId\"", page);
        Assert.Contains("data-cd-handler-url=", page);
    }
}
