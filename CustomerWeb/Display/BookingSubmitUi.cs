namespace CustomerWeb.Display;

/// Maps booking-create API errors to user-facing Vietnamese copy. Does not change API/DB.
public static class BookingSubmitUi
{
    public const string ValidationSummary = "Vui lòng kiểm tra lại thông tin đặt xe.";
    public const string GenericFailure = "Không thể tạo đơn thuê. Vui lòng thử lại.";
    public const string FailureTitle = "Đặt xe không thành công.";
    public const string NeedQuoteReview =
        "Vui lòng kiểm tra báo giá bên dưới, rồi bấm «Gửi yêu cầu đặt xe» để gửi đơn.";

    public static string FriendlyCreateFailure(string? apiMessage)
    {
        if (string.IsNullOrWhiteSpace(apiMessage))
            return GenericFailure;

        var msg = apiMessage.Trim();
        if (LooksTechnical(msg))
            return GenericFailure;

        if (msg.Equals("Dat xe that bai.", StringComparison.OrdinalIgnoreCase)
            || msg.Equals("Phan hoi khong hop le.", StringComparison.OrdinalIgnoreCase)
            || msg.Equals("Đặt xe thất bại.", StringComparison.OrdinalIgnoreCase))
            return GenericFailure;

        return msg;
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
