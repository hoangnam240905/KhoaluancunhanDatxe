using System.Globalization;
using CustomerWeb.Models;

namespace CustomerWeb.Display;

/// Create Booking presentation helpers: catalog slug → existing VehicleTypeId, VND formatting.
public static class CreateBookingUi
{
    public static DateTime DefaultPickup => DateTime.Now.Date.AddDays(1).AddHours(10);
    public static DateTime DefaultReturn => DefaultPickup.AddDays(3);
    public const string DefaultPickupPlace = "Tân Sơn Nhất, TP. Hồ Chí Minh";
    public const string SameReturnPlace = "Giống địa điểm nhận xe";
    public const decimal DepositPercent = 50;

    public static IReadOnlyList<CreateBookingAddon> Addons { get; } =
    [
        new("insurance", "bi-shield-check", "Bảo hiểm bổ sung", "Bảo vệ thêm cho chuyến đi của bạn.", 150_000m),
        new("gps", "bi-geo-alt", "GPS / Dẫn đường", "Hỗ trợ dẫn đường trên hành trình.", 50_000m),
        new("child-seat", "bi-emoji-smile", "Ghế trẻ em", "Ghế an toàn cho trẻ nhỏ.", 80_000m),
        new("extra-driver", "bi-person-plus", "Tài xế bổ sung", "Thêm người lái luân phiên cho hành trình dài.", 200_000m),
    ];

    /// Maps the presentation catalog card onto an existing API VehicleTypeId.
    /// Does not invent IDs — returns 0 when the type list is empty.
    public static int ResolveTypeId(
        CatalogVehicle vehicle,
        int? requestedTypeId,
        IReadOnlyList<VehicleTypeResponse> types)
    {
        if (types is not { Count: > 0 }) return 0;

        if (requestedTypeId is int id && types.Any(t => t.TypeId == id))
            return id;

        var category = vehicle.Category;
        var matched = types.Where(t =>
            t.TypeName.Contains(category, StringComparison.OrdinalIgnoreCase)
            || category.Contains(CategoryOf(t.TypeName, t.SeatCapacity), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var pool = matched.Count > 0 ? matched : types;
        return pool
            .OrderBy(t => Math.Abs(t.SeatCapacity - vehicle.Seats))
            .ThenBy(t => t.TypeId)
            .First()
            .TypeId;
    }

    public static CatalogVehicle Unspecified() => new(
        "select",
        "Chưa chọn xe",
        "",
        0,
        "",
        "",
        0,
        0m,
        0,
        0m,
        "/images/home/sedan.jpg",
        null,
        "Hãy chọn một xe cụ thể từ tìm kiếm hoặc danh mục.",
        "",
        [],
        [],
        ["/images/home/sedan.jpg"],
        ["Tự lái", "Có tài xế"]);

    public static CatalogVehicle Resolve(string? slug, int? typeId, IReadOnlyList<VehicleTypeResponse>? types)
    {
        if (typeId is int id && types is { Count: > 0 })
        {
            var type = types.FirstOrDefault(t => t.TypeId == id);
            if (type is not null)
                return FromType(type);
        }

        return Unspecified();
    }

    public static int? ParseVehicleId(string? slug, int? vehicleId)
    {
        if (vehicleId is int id && id > 0)
            return id;
        if (int.TryParse(slug, out var fromSlug) && fromSlug > 0)
            return fromSlug;
        return null;
    }

    public static IReadOnlyList<CatalogVehicle> FromVehicles(
        IReadOnlyList<VehicleResponse> vehicles,
        IReadOnlyList<VehicleTypeResponse> types)
        => vehicles
            .OrderBy(v => v.TypeId)
            .ThenBy(v => v.VehicleId)
            .Select(v => FromApi(v, types.FirstOrDefault(t => t.TypeId == v.TypeId)))
            .ToList();

    public static SearchVehicleCard ToSearchCard(
        CatalogVehicle vehicle,
        bool isAvailableForPeriod = true,
        DateTime? conflictStart = null,
        DateTime? conflictEnd = null,
        DateTime? availableFrom = null)
        => new(
            vehicle.Name,
            vehicle.Category,
            vehicle.Seats,
            vehicle.Transmission,
            vehicle.Fuel,
            vehicle.Rating,
            vehicle.ReviewCount,
            vehicle.PricePerDay,
            vehicle.ImageUrl,
            vehicle.Badge,
            vehicle.Slug,
            vehicle.LicensePlate,
            vehicle.VehicleId,
            isAvailableForPeriod,
            conflictStart,
            conflictEnd,
            availableFrom);

    public static CatalogVehicle? TryMapUniqueBrandModel(
        string? slug,
        IReadOnlyList<VehicleResponse> vehicles,
        IReadOnlyList<VehicleTypeResponse> types)
    {
        var mock = CatalogUi.Find(slug);
        if (mock is null || vehicles.Count == 0)
            return null;

        var matches = vehicles
            .Where(v => string.Equals($"{v.Brand} {v.Model}".Trim(), mock.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count != 1)
            return null;

        var row = matches[0];
        return FromApi(row, types.FirstOrDefault(t => t.TypeId == row.TypeId));
    }

    public static CatalogVehicle FromApi(VehicleResponse vehicle, VehicleTypeResponse? type)
    {
        var seats = type is { SeatCapacity: > 0 }
            ? type.SeatCapacity
            : vehicle.SeatCapacity;
        var typeName = type?.TypeName ?? vehicle.TypeName;
        var category = CategoryOf(typeName, seats);
        var name = $"{vehicle.Brand} {vehicle.Model}".Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = vehicle.LicensePlate;
        var image = HomeUi.VehicleImage(type?.ImageUrl, typeName, seats);
        if (image.Contains("van.jpg", StringComparison.OrdinalIgnoreCase))
            image = "/images/home/limousine.jpg";
        var price = type is { PricePerDay: > 0 } ? type.PricePerDay : vehicle.PricePerDay;
        var plate = vehicle.LicensePlate;

        return new(
            vehicle.VehicleId.ToString(),
            name,
            category,
            seats,
            "",
            "",
            0,
            0m,
            0,
            price,
            image,
            null,
            $"{category} | {plate}",
            $"{name} · biển số {plate}.",
            [plate, vehicle.Brand, category],
            [],
            [image],
            ["Tự lái", "Có tài xế"],
            vehicle.VehicleId,
            plate,
            vehicle.Brand,
            vehicle.Model);
    }

    public static string Money(decimal amount) =>
        $"{amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} VNĐ";

    private static CatalogVehicle FromType(VehicleTypeResponse type)
    {
        var image = HomeUi.VehicleImage(type.ImageUrl, type.TypeName, type.SeatCapacity);
        if (image.Contains("van.jpg", StringComparison.OrdinalIgnoreCase))
            image = "/images/home/limousine.jpg";

        var category = CategoryOf(type.TypeName, type.SeatCapacity);
        return new(
            "selected",
            type.TypeName,
            category,
            type.SeatCapacity,
            "",
            "",
            0,
            0m,
            0,
            type.PricePerDay,
            image,
            null,
            $"{category} | Phù hợp cho hành trình của bạn.",
            type.Description ?? "",
            [],
            [],
            [image],
            ["Tự lái", "Có tài xế"]);
    }

    private static string CategoryOf(string? name, int seats)
    {
        var n = (name ?? "").ToLowerInvariant();
        if (n.Contains("limousine") || n.Contains("limo")) return "Limousine";
        if (n.Contains("suv")) return "SUV";
        if (n.Contains("mpv")) return "MPV";
        if (n.Contains("van")) return "Van";
        if (n.Contains("sedan")) return "Sedan";
        return seats >= 9 ? "Van" : seats >= 6 ? "SUV" : "Sedan";
    }
}

public sealed record CreateBookingAddon(
    string Id,
    string Icon,
    string Title,
    string Description,
    decimal Price);
