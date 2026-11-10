using Xunit;

namespace Backend.Tests;

/// <summary>
/// Dispatcher handover UI in PortalWeb (Vận hành chuyến + Hub). The old /Dispatcher pages and the separate
/// DispatcherWeb were removed (plan1.md mục H); these checks moved here.
/// </summary>
public class PhaseDispatcherHandoverUiTests
{
    [Fact]
    public void Trips_and_hub_handover_use_vehicle_state_validate_km_fuel_and_require_licence_check()
    {
        var tripsCode = Read("PortalWeb", "Pages", "Mockup", "Trips.cshtml.cs");
        var hubCode = Read("PortalWeb", "Pages", "Mockup", "Hub.cshtml.cs");
        var hubJs = Read("PortalWeb", "wwwroot", "js", "mockup-hub.js");
        var tripsJs = Read("PortalWeb", "wwwroot", "js", "mockup-trips.js");

        foreach (var code in new[] { tripsCode, hubCode })
        {
            Assert.Contains("HandoverVehicleState", code);
            Assert.Contains("ResolveHandoverCondition", code);
            Assert.Contains("OriginalLicenseChecked = originalLicenseChecked", code);
        }
        Assert.DoesNotContain("if (!currentKm)", hubJs);
        Assert.Contains("isValidKm", hubJs);
        Assert.Contains("isValidFuelPercent", hubJs);
        Assert.Contains("data-handover-licence-check", hubJs);
        Assert.Contains("data-handover-licence-check", tripsJs);
        Assert.False(Directory.Exists(Path.Combine(RepoRoot(), "PortalWeb", "Pages", "Dispatcher")));
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([RepoRoot(), .. parts]));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PortalWeb", "PortalWeb.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate CarRentalSystem repo root.");
    }
}
