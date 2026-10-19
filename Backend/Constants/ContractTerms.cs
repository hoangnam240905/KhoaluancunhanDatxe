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
        "Khi đơn bị hủy, hợp đồng hết hiệu lực và tài xế (nếu có) được thông báo ngay."
    ];
}
