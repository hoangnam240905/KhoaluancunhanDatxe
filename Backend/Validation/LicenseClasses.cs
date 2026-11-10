namespace Backend.Validation;

/// <summary>
/// Driving-licence classes for cars, following Luật Trật tự, an toàn giao thông đường bộ số 36/2024/QH15
/// (Điều 57, from 01/01/2025): B = cars up to 8 seats not counting the driver, D1 = over 8 up to 16,
/// D2 = over 16 up to 29, D = over 29; C1 / C also cover the B vehicles. Licences issued before 2025 stay
/// valid until they expire (Điều 89): old B1 / B2 / C cover up to 8 seats, old D up to 29, old E above.
/// A B1 issued from 2025 is a motorcycle licence and drives no car. Shared by drivers (plan1.md Phần D)
/// and self-drive customers (Phần E). Check the wording against the law text before quoting it in the thesis.
/// </summary>
public static class LicenseClasses
{
    public sealed record Info(string Code, string Label, int MaxSeats, bool Professional);

    /// <summary>Seats not counting the driver that each class may carry; 0 = not a car licence.</summary>
    public static readonly IReadOnlyList<Info> All =
    [
        new("B", "B", 8, true),
        new("C1", "C1", 8, true),
        new("C", "C", 8, true),
        new("D1", "D1", 16, true),
        new("D2", "D2", 29, true),
        new("D", "D", 999, true),
        // Issued before 01/01/2025, still valid until expiry.
        new("B1-cu", "B1 (cấp trước 01/01/2025)", 8, false),
        new("B2", "B2 (cũ)", 8, true),
        new("C-cu", "C (cũ)", 8, true),
        new("D-cu", "D (cũ)", 29, true),
        new("E-cu", "E (cũ)", 999, true),
        new("B1", "B1 (cấp từ 2025 — mô tô)", 0, false)
    ];

    /// <summary>Minimum classes a vehicle type can require (new law names).</summary>
    public static readonly IReadOnlyList<string> VehicleMinimums = ["B", "D1", "D2", "D"];

    public static Info? Find(string? code)
        => All.FirstOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Label(string? code) => Find(code)?.Label ?? code ?? "";

    /// <summary>Minimum class for a vehicle with this many seats (including the driver's seat).</summary>
    public static string MinimumForSeats(int seatCapacity)
    {
        var passengers = Math.Max(0, seatCapacity - 1);
        return passengers <= 8 ? "B" : passengers <= 16 ? "D1" : passengers <= 29 ? "D2" : "D";
    }

    /// <summary>Why this licence cannot drive the vehicle type; null when it can.</summary>
    /// <param name="professional">Company drivers cannot rely on an old B1 (not a professional licence).</param>
    public static string? CannotDrive(string? licenceClass, string? vehicleMinimum, int seatCapacity, bool professional)
    {
        var minimum = Find(vehicleMinimum) is { } m && VehicleMinimums.Contains(m.Code) ? m.Code : MinimumForSeats(seatCapacity);
        var needed = Find(minimum)!.MaxSeats;
        var held = Find(licenceClass);
        if (held is null)
            return "Chưa khai hạng bằng lái.";
        if (professional && !held.Professional)
            return $"Bằng {held.Label} không dùng để lái xe kinh doanh vận tải.";
        if (held.MaxSeats < needed)
            return $"Bằng {held.Label} không lái được xe {seatCapacity} chỗ (cần {minimum} trở lên).";
        return null;
    }
}
