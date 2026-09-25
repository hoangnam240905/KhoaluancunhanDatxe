using Xunit;

namespace Backend.Tests;

public class PhaseDispatchFleetUiTests
{
    [Fact]
    public void Portal_dispatcher_index_shows_fleet_status_board()
    {
        var view = Read("PortalWeb", "Pages", "Dispatcher", "Index.cshtml");
        var code = Read("PortalWeb", "Pages", "Dispatcher", "Index.cshtml.cs");
        Assert.Contains("GetFleetStatusAsync", code);
        Assert.Contains("Tình trạng xe", view);
        Assert.Contains("LicensePlate", view);
        Assert.Contains("VehicleStatus", view);
        Assert.Contains("RentalMode", view);
        Assert.Contains("SelfDrive không gắn tài xế", view);
    }

    [Fact]
    public void DispatcherWeb_index_shows_fleet_status_board()
    {
        var view = Read("DispatcherWeb", "Pages", "Index.cshtml");
        var code = Read("DispatcherWeb", "Pages", "Index.cshtml.cs");
        Assert.Contains("GetFleetStatusAsync", code);
        Assert.Contains("Tình trạng xe", view);
        Assert.Contains("GetAssignableAsync", Read("DispatcherWeb", "Pages", "Assign.cshtml.cs"));
        Assert.Contains("GetAssignableAsync", Read("PortalWeb", "Pages", "Dispatcher", "Assign.cshtml.cs"));
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
