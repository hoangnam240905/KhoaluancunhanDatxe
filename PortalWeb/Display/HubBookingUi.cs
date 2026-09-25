using System.Globalization;
using PortalWeb.Models;

namespace PortalWeb.Display;

/// <summary>Maps Backend BookingResponse into Mockup Hub row shape (client JS).</summary>
public static class HubBookingUi
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static object ToHubRow(BookingResponse b, string? contractStatus = null)
    {
        var vehicle = FormatVehicle(b);
        var plate = b.AssignedVehicle?.LicensePlate
            ?? b.Assignment?.LicensePlate
            ?? "—";
        var held = b.AssignedVehicle is not null || b.Assignment is not null;
        var driverAssigned = b.Assignment is not null;
        var depositKey = b.HasActiveDeposit ? "paid" : (b.QuotedDepositAmount is > 0 ? "pending" : "none");
        var contractKey = MapContract(contractStatus);
        var price = b.FinalBaseAmount ?? (b.QuotedPricePerDay is decimal d && b.QuotedDays is int days ? d * days : b.TotalAmount);
        var depositAmt = b.QuotedDepositAmount ?? 0m;
        var total = b.FinalAmount ?? b.TotalAmount;

        return new
        {
            id = b.BookingId,
            created = Fmt(b.CreatedAt),
            customer = b.CustomerName,
            phone = "—",
            email = "—",
            mode = string.IsNullOrWhiteSpace(b.RentalMode) ? "WithDriver" : b.RentalMode,
            vehicle,
            type = b.VehicleTypeName,
            plate,
            start = Fmt(b.StartDate),
            end = Fmt(b.EndDate),
            startIso = b.StartDate.ToString("yyyy-MM-dd"),
            endIso = b.EndDate.ToString("yyyy-MM-dd"),
            pickup = b.PickupAddress,
            dropoff = b.DropoffAddress,
            contract = contractKey,
            deposit = depositKey,
            status = b.Status,
            price = Money(price),
            depositAmt = Money(depositAmt),
            total = Money(total),
            vehicleHeld = held,
            driverAssigned
        };
    }

    private static string FormatVehicle(BookingResponse b)
    {
        if (b.AssignedVehicle is not null)
            return $"{b.AssignedVehicle.Brand} {b.AssignedVehicle.Model}".Trim();
        return b.VehicleTypeName;
    }

    private static string MapContract(string? status) => status?.ToLowerInvariant() switch
    {
        "issued" => "issued",
        "signed" => "signed",
        "voided" => "none",
        _ => "none"
    };

    private static string Fmt(DateTime dt) => dt.ToString("dd/MM/yyyy HH:mm", Vi);
    private static string Money(decimal v) => v.ToString("#,0", Vi);
}
