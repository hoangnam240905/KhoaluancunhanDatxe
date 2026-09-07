namespace Backend.Validation;

public static class VehicleOccupancyRules
{
    public const string CannotDelete =
        "Không thể xóa xe đang được sử dụng hoặc đang được giữ cho đơn thuê.";

    public const string CannotDeactivate =
        "Không thể vô hiệu hóa xe đang được sử dụng hoặc đang được giữ cho đơn thuê.";

    public const string CannotDeleteHistoric =
        "Không thể xóa xe đã được sử dụng.";
}
