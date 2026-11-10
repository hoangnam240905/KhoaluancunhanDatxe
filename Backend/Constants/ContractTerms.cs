namespace Backend.Constants;

/// <summary>
/// Contract wording. ContractService stores the built sentences verbatim in Contract.TermsSnapshot at issue time;
/// issued contracts are never re-rendered from here.
/// </summary>
public static class ContractTerms
{
    public static IReadOnlyList<string> Build(int cancelMinHours) =>
    [
        "Khách hàng sử dụng xe đúng mục đích thuê.",
        "Thời gian nhận và trả xe được thực hiện theo thông tin đơn thuê.",
        "Các khoản phát sinh được xử lý theo chính sách của hệ thống.",
        "Khách hàng có trách nhiệm kiểm tra tình trạng xe khi nhận và trả xe.",
        "Khách hàng được hủy đơn chưa phân công xe/tài xế bất kỳ lúc nào trước giờ nhận xe.",
        $"Đơn đã phân công xe/tài xế: khách hàng tự hủy trước giờ nhận xe tối thiểu {cancelMinHours} giờ; sau mốc này vui lòng liên hệ điều phối.",
        "Hủy đúng hạn được hoàn 100% tiền cọc; việc hoàn tiền được xử lý thủ công.",
        "Đơn do điều phối hoặc hệ thống hủy được hoàn 100% tiền cọc.",
        "Khi đơn bị hủy, hợp đồng hết hiệu lực và tài xế (nếu có) được thông báo ngay.",
        // plan1.md Phần C: changing the vehicle during the rental, and the self-drive licence (Phần E).
        "Trong thời gian thuê, khách hàng được yêu cầu đổi xe khi xe gặp sự cố kỹ thuật hoặc khi nhu cầu sử dụng thay đổi; việc đổi xe chỉ được thực hiện khi điều phối viên phê duyệt.",
        "Xe thay thế phải còn trống lịch trong toàn bộ thời gian thuê còn lại và đáp ứng yêu cầu của đơn thuê (loại xe, số chỗ; với đơn tự lái thì phù hợp hạng bằng lái của khách) trước khi được duyệt.",
        "Chênh lệch giá thuê giữa xe cũ và xe thay thế (nếu có) được thông báo cho khách hàng và chỉ áp dụng sau khi khách hàng xác nhận.",
        "Việc đổi xe được ghi nhận bằng phụ lục hợp đồng (thông tin xe thay thế, thời điểm đổi, chênh lệch giá nếu có, người phê duyệt) và không làm mất hiệu lực của hợp đồng gốc.",
        "Với đơn tự lái, khách hàng phải có bằng lái phù hợp đã được điều phối viên duyệt và xuất trình bằng lái gốc khi nhận xe; không đáp ứng thì không giao xe."
    ];
}
