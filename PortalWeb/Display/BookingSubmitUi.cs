namespace PortalWeb.Display;

/// Maps booking-create API errors to user-facing Vietnamese copy. Does not change API/DB.
public static class BookingSubmitUi
{
    public const string ValidationSummary = "Vui lòng kiểm tra lại thông tin đặt xe.";
    public const string GenericFailure = "Không thể tạo đơn thuê. Vui lòng thử lại.";
    public const string FailureTitle = "Đặt xe không thành công.";
    public const string NeedQuoteReview =
        "Vui lòng kiểm tra báo giá bên dưới, rồi bấm «Xác nhận đặt xe» để gửi đơn.";
    public const string QuoteUnavailable = "Không thể tính báo giá. Vui lòng kiểm tra lại thông tin.";
    public const string VehicleUnavailable = "Xe này hiện không khả dụng trong khoảng thời gian bạn đã chọn.";
    public const string QuoteLoading = "Đang tính giá...";
    public const string AvailabilityLoading = "Đang kiểm tra tình trạng xe...";
    public const string Creating = "Đang tạo đơn...";

    public static string FriendlyCreateFailure(string? apiMessage)
    {
        if (string.IsNullOrWhiteSpace(apiMessage))
            return GenericFailure;

        var msg = apiMessage.Trim();
        if (LooksUnavailable(msg))
            return VehicleUnavailable;
        if (LooksTechnical(msg))
            return GenericFailure;

        if (msg.Equals("Dat xe that bai.", StringComparison.OrdinalIgnoreCase)
            || msg.Equals("Phan hoi khong hop le.", StringComparison.OrdinalIgnoreCase)
            || msg.Equals("Đặt xe thất bại.", StringComparison.OrdinalIgnoreCase))
            return GenericFailure;

        return msg;
    }

    public static string FriendlyQuoteFailure(string? apiMessage)
    {
        if (string.IsNullOrWhiteSpace(apiMessage))
            return QuoteUnavailable;

        var msg = apiMessage.Trim();
        if (LooksUnavailable(msg))
            return VehicleUnavailable;
        if (LooksTechnical(msg))
            return QuoteUnavailable;

        return msg;
    }

    private static bool LooksUnavailable(string msg)
    {
        return msg.Contains("không còn khả dụng", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("không khả dụng", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("khong con kha dung", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("khong kha dung", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("xung đột", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("xung dot", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("conflict", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("buffer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksTechnical(string msg)
    {
        if (msg.StartsWith("Lỗi API", StringComparison.OrdinalIgnoreCase)) return true;
        if (msg.StartsWith("Loi API", StringComparison.OrdinalIgnoreCase)) return true;
        if (msg.StartsWith("Lỗi ", StringComparison.OrdinalIgnoreCase)
            && msg.Length <= 16
            && msg.Skip(5).All(c => char.IsDigit(c) || c is '(' or ')'))
            return true;
        if (msg.Contains("Exception", StringComparison.OrdinalIgnoreCase)) return true;
        if (msg.Contains("StackTrace", StringComparison.OrdinalIgnoreCase)) return true;
        if (msg.Contains("at System.", StringComparison.OrdinalIgnoreCase)) return true;
        if (msg.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase)) return true;
        return msg.Length > 280;
    }
}
