using System.Globalization;
using PortalWeb.Models;

namespace PortalWeb.Display;

/// <summary>Maps Backend inspection + booking/vehicle data into Mockup Inspections row shapes.</summary>
public static class InspectionsUi
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static object ToRow(
        VehicleInspectionResponse i,
        BookingResponse? booking,
        VehicleResponse? vehicle,
        VehicleInspectionResponse? pairedHandover)
    {
        var type = MapType(i.InspectionType);
        var exterior = DisplayCond(i.ExteriorCondition) ?? DisplayCond(i.Condition) ?? "—";
        var technical = DisplayCond(i.TechnicalCondition) ?? "—";
        var conditionKey = MapConditionKey(exterior, technical, i.Condition);
        var plate = booking?.AssignedVehicle?.LicensePlate
            ?? vehicle?.LicensePlate
            ?? "—";
        var vehicleName = booking?.AssignedVehicle is { } av
            ? $"{av.Brand} {av.Model}".Trim()
            : vehicle is not null
                ? $"{vehicle.Brand} {vehicle.Model}".Trim()
                : (booking?.VehicleTypeName ?? $"Xe #{i.VehicleId}");

        var km = i.OdometerKm is decimal odo ? (int)odo : 0;
        var fuel = i.FuelLevel is decimal f ? (int)f : 0;
        int? kmOut = null;
        int? fuelOut = null;
        object? checkoutRef = null;
        if (type == "checkin" && pairedHandover is not null)
        {
            kmOut = pairedHandover.OdometerKm is decimal ho ? (int)ho : null;
            fuelOut = pairedHandover.FuelLevel is decimal hf ? (int)hf : null;
            checkoutRef = new
            {
                km = kmOut ?? 0,
                fuel = fuelOut ?? 0,
                exterior = DisplayCond(pairedHandover.ExteriorCondition) ?? DisplayCond(pairedHandover.Condition) ?? "—",
                technical = DisplayCond(pairedHandover.TechnicalCondition) ?? "—"
            };
        }

        var mode = booking is null
            ? "—"
            : string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase) ? "Tự lái" : "Có tài xế";
        var driver = booking?.Assignment?.DriverName
            ?? (string.Equals(booking?.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase) ? "Khách tự lái" : "—");

        return new
        {
            id = i.InspectionId,
            bookingId = i.BookingId,
            booking = "#" + i.BookingId,
            vehicleId = i.VehicleId,
            plate,
            vehicle = vehicleName,
            type,
            inspectionType = i.InspectionType,
            km,
            fuel,
            kmOut,
            fuelOut,
            condition = conditionKey,
            datetime = Fmt(i.ActualAt),
            dateIso = i.ActualAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            inspector = "—", // not in VehicleInspectionResponse
            customer = booking?.CustomerName ?? "—",
            driver,
            mode,
            rentFrom = booking is null ? "—" : FmtDate(booking.StartDate),
            rentTo = booking is null ? "—" : FmtDate(booking.EndDate),
            exterior,
            interior = "—", // not in API
            tires = "—", // not in API
            technical,
            notes = string.IsNullOrWhiteSpace(i.Notes) ? "—" : i.Notes,
            issue = BuildIssue(conditionKey, exterior, technical, i.Notes),
            checkoutRef,
            // No checklist / photo APIs — keep empty real arrays (UI marks as unavailable).
            checklist = (object?)null,
            photos = Array.Empty<string>(),
            apiSource = true
        };
    }

    public static string MapType(string? inspectionType) =>
        string.Equals(inspectionType, "Return", StringComparison.OrdinalIgnoreCase) ? "checkin" : "checkout";

    private static string? DisplayCond(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return t switch
        {
            "Good" or "Tốt" or "OK" => "Tốt",
            "Minor" or "Vấn đề nhỏ" => "Vấn đề nhỏ",
            "Inspect" or "Cần kiểm tra" or "NeedsInspection" => "Cần kiểm tra",
            "Major" or "Vấn đề nghiêm trọng" or "Damaged" => "Vấn đề nghiêm trọng",
            _ => t
        };
    }

    private static string MapConditionKey(string exterior, string technical, string? condition)
    {
        static int Rank(string? s) => s switch
        {
            "Vấn đề nghiêm trọng" => 3,
            "Cần kiểm tra" => 2,
            "Vấn đề nhỏ" => 1,
            _ => 0
        };
        var fromParts = Math.Max(Rank(exterior), Rank(technical));
        var fromCond = Rank(DisplayCond(condition));
        var rank = Math.Max(fromParts, fromCond);
        return rank switch
        {
            3 => "major",
            2 => "inspect",
            1 => "minor",
            _ => "good"
        };
    }

    private static object? BuildIssue(string conditionKey, string exterior, string technical, string? notes)
    {
        if (conditionKey == "good") return null;
        var level = conditionKey switch
        {
            "major" => "Nghiêm trọng",
            "inspect" => "Cần kiểm tra",
            _ => "Nhỏ"
        };
        var title = exterior is not ("Tốt" or "—") ? $"Ngoại thất: {exterior}"
            : technical is not ("Tốt" or "—") ? $"Kỹ thuật: {technical}"
            : "Vấn đề được ghi nhận";
        return new
        {
            title,
            level,
            note = string.IsNullOrWhiteSpace(notes) ? "Từ biên bản Backend." : notes
        };
    }

    private static string Fmt(DateTime dt) => dt.ToString("dd/MM/yyyy HH:mm", Vi);
    private static string FmtDate(DateTime dt) => dt.ToString("dd/MM/yyyy", Vi);
}
