using Backend.Constants;
using Backend.DTOs.Chatbot;
using Backend.Services;
using Backend.Services.Chatbot;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md 1.7 — extended chatbot test set (K-A search/price, K-B app FAQ, K-C "chưa có thông tin")
/// with rules R1 people, R2 next month, R3 daily budget, R4 SUV, R5 length without dates.
/// </summary>
public class ChatbotExtendedTests
{
    // Seed fleet: 1 Vios, 2 Accent (4 chỗ - Sedan); 3 Fortuner, 4 Everest (7 chỗ - SUV); 5 Solati (16 chỗ); 6 Transit (Limousine 9 chỗ).
    private static readonly DateTime Now = new(2027, 3, 10, 10, 0, 0);
    private static readonly FaqKnowledgeBase Faq = FaqKnowledgeBase.Load(KnowledgePath());

    // ---------- parser rules ----------

    [Fact]
    public void R1_R3_R5_people_daily_budget_and_length_are_read_without_mistaking_them_for_dates()
    {
        var q = ChatQueryParser.Parse("Nhà tôi có 4 người, muốn thuê xe đi Vũng Tàu 2 ngày 1 đêm", Now);
        Assert.Equal((4, 2), (q.People, q.Days));
        Assert.Null(q.Start); // "ngày 1" inside "2 ngày 1 đêm" is not the 1st of the month

        var b = ChatQueryParser.Parse("ngân sách khoảng 1,5 triệu đồng/ngày", Now);
        Assert.Equal((1_500_000m, true, (int?)null), (b.MaxPrice, b.BudgetPerDay, b.Days));

        Assert.Equal(1, ChatQueryParser.Parse("Thuê xe 4 chỗ một ngày giá bao nhiêu?", Now).Days);
        Assert.False(ChatQueryParser.Parse("xe 4 chỗ từ 22 đến 25 dưới 4 triệu", Now).BudgetPerDay);
    }

    [Fact]
    public void R2_next_month_moves_dates_without_a_month()
    {
        var q = ChatQueryParser.Parse("Ngày 15 đến 17 tháng sau còn xe 7 chỗ không?", Now);
        Assert.Equal(new DateTime(2027, 4, 15, 8, 0, 0), q.Start);
        Assert.Equal(new DateTime(2027, 4, 17, 8, 0, 0), q.End);
    }

    [Theory]
    [InlineData("Hủy đơn trước 1 ngày được không?")]
    [InlineData("Tôi muốn gia hạn thêm 1 ngày")]
    public void A_length_alone_is_not_a_search(string text) => Assert.False(ChatQueryParser.Parse(text, Now).IsSearch);

    // ---------- K-A: search, price, availability ----------

    [Fact]
    public async Task K_A01_four_people_two_days_gives_a_4_seat_estimate_and_asks_dates()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Nhà tôi có 4 người, muốn thuê xe đi Vũng Tàu 2 ngày 1 đêm thì nên chọn xe nào?");
        Assert.Equal("ask", r.Kind);
        Assert.Contains("Giá ước tính 2 ngày", r.Reply);
        Assert.Contains("4 chỗ - Sedan", r.Reply);
        Assert.DoesNotContain("7 chỗ", r.Reply);
        Assert.Contains(Money(Quote(iso, 1, RentalModes.WithDriver, 2).TotalAmount), r.Reply);
        Assert.Contains("ngày nào", r.Reply);
    }

    [Fact]
    public async Task K_A02_seven_or_sixteen_for_ten_people_says_seven_is_too_small()
    {
        using var iso = Setup();
        var noDate = await Ask(iso, "Bên bạn có xe 7 chỗ hoặc 16 chỗ cho đoàn 10 người không?");
        Assert.Equal("ask", noDate.Kind);
        Assert.Contains("Xe 7 chỗ không đủ chỗ cho 10 người", noDate.Reply);

        var withDate = await Ask(iso, "Từ 22 đến 25 có xe 7 chỗ hoặc 16 chỗ cho đoàn 10 người không?");
        Assert.Equal("search", withDate.Kind);
        Assert.Contains(withDate.Vehicles, v => v.Seats == 16);
        Assert.Contains("không đủ chỗ cho 10 người", withDate.Reply);
    }

    [Fact]
    public async Task K_A03_advice_between_4_and_7_asks_people_and_never_picks_a_car()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Tôi chưa biết nên thuê 4 chỗ hay 7 chỗ, tư vấn giúp tôi với.");
        Assert.Equal(("ask", ChatbotService.AskPeopleAndDates), (r.Kind, r.Reply));
        Assert.Empty(r.Vehicles);
    }

    [Fact]
    public async Task K_A04_high_clearance_means_suv_types_only()
    {
        using var iso = Setup();
        Assert.Equal("ask", (await Ask(iso, "Tôi muốn thuê xe gầm cao để đi Đà Lạt.")).Kind);
        var r = await Ask(iso, "Từ 22 đến 25 tôi muốn thuê xe gầm cao đi Đà Lạt");
        Assert.Equal("search", r.Kind);
        Assert.NotEmpty(r.Vehicles);
        Assert.All(r.Vehicles, v => Assert.Contains("SUV", v.TypeName));
    }

    [Theory]
    [InlineData("Tôi muốn xem tất cả xe đang cho thuê.", "trang Tìm xe")]
    [InlineData("Có xe máy cho thuê không?", "không cho thuê xe máy")]
    [InlineData("Có phát sinh phí vượt số kilomet không?", "km miễn phí")]
    public async Task K_A05_A06_A12_answer_from_the_faq(string question, string expected)
    {
        using var iso = Setup();
        var r = await Ask(iso, question);
        Assert.Equal("faq", r.Kind);
        Assert.Contains(expected, r.Reply);
    }

    [Theory]
    [InlineData("Xe nào tiết kiệm chi phí nhất cho chuyến đi của tôi?")]
    [InlineData("Tôi muốn thuê xe với ngân sách khoảng 1,5 triệu đồng/ngày, có xe nào phù hợp không?")]
    public async Task K_A07_A13_without_people_or_dates_ask_for_them(string question)
    {
        using var iso = Setup();
        var r = await Ask(iso, question);
        Assert.Equal(("ask", ChatbotService.AskPeopleAndDates), (r.Kind, r.Reply));
    }

    [Fact]
    public async Task K_A07_with_details_lists_cheapest_first()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Từ 22 đến 25, 4 người, xe nào tiết kiệm nhất?");
        Assert.Equal("search", r.Kind);
        Assert.Equal(r.Vehicles.OrderBy(v => v.TotalAmount).Select(v => v.VehicleId), r.Vehicles.Select(v => v.VehicleId));
    }

    [Theory]
    [InlineData("Thuê xe 4 chỗ một ngày giá bao nhiêu?", 1, 1)]
    [InlineData("Thuê xe 7 chỗ đi Đà Lạt 3 ngày hết bao nhiêu tiền?", 2, 3)]
    public async Task K_A08_A09_length_without_dates_gives_a_with_driver_estimate(string question, int typeId, int days)
    {
        using var iso = Setup();
        var r = await Ask(iso, question);
        Assert.Equal("ask", r.Kind);
        Assert.Contains($"Giá ước tính {days} ngày", r.Reply);
        Assert.Contains("có tài xế, chưa gồm cước km", r.Reply);
        Assert.Contains(Money(Quote(iso, typeId, RentalModes.WithDriver, days).TotalAmount), r.Reply);
    }

    [Fact]
    public async Task K_A10_self_drive_two_days_needs_a_size_then_quotes_with_free_km()
    {
        using var iso = Setup();
        Assert.Equal(("ask", ChatbotService.AskPeople), ((await Ask(iso, "Tôi thuê xe tự lái 2 ngày thì tổng chi phí là bao nhiêu?")) is var a ? (a.Kind, a.Reply) : default));

        var r = await Ask(iso, "Tôi thuê xe 7 chỗ tự lái 2 ngày thì tổng chi phí là bao nhiêu?");
        Assert.Contains(Money(Quote(iso, 2, RentalModes.SelfDrive, 2).TotalAmount), r.Reply);
        Assert.Contains("km/ngày", r.Reply);
    }

    [Fact]
    public async Task K_A11_compare_modes_quotes_both()
    {
        using var iso = Setup();
        Assert.Equal("ask", (await Ask(iso, "Thuê xe có tài xế và tự lái thì phương án nào rẻ hơn?")).Kind);

        var r = await Ask(iso, "Xe 7 chỗ 3 ngày, có tài xế và tự lái thì phương án nào rẻ hơn?");
        Assert.Contains(Money(Quote(iso, 2, RentalModes.SelfDrive, 3).TotalAmount), r.Reply);
        Assert.Contains(Money(Quote(iso, 2, RentalModes.WithDriver, 3).TotalAmount), r.Reply);
        Assert.Contains("rẻ hơn", r.Reply);
    }

    [Fact]
    public async Task K_A13_daily_budget_is_multiplied_by_the_rental_days()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Từ 22 đến 25 xe 4 chỗ ngân sách 1 triệu/ngày");
        Assert.Equal("search", r.Kind);
        Assert.Contains("1.000.000 đ/ngày × 3 ngày = 3.000.000 đ", r.Reply);
        Assert.All(r.Vehicles, v => Assert.True(v.TotalAmount <= 3_000_000m));
    }

    [Fact]
    public async Task K_A14_long_message_estimates_seven_seats_self_drive_and_asks_a_concrete_date()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Cuối tuần sau gia đình tôi 6 người muốn đi Đà Lạt 3 ngày 2 đêm, thuê xe tự lái, ngân sách khoảng 1,5 triệu/ngày. Cho tôi biết tổng chi phí, tiền cọc và giấy tờ cần chuẩn bị.");
        Assert.Equal("ask", r.Kind);
        var quote = Quote(iso, 2, RentalModes.SelfDrive, 3);
        Assert.Contains("7 chỗ - SUV", r.Reply);
        Assert.Contains(Money(quote.TotalAmount), r.Reply);
        Assert.Contains($"cọc 50%: {Money(quote.DepositAmount)}", r.Reply);
        Assert.Contains("1.500.000 đ/ngày × 3 ngày = 4.500.000 đ", r.Reply);
        Assert.Contains(ChatbotService.AskConcreteDate, r.Reply);
        Assert.DoesNotContain("giấy tờ", r.Reply, StringComparison.OrdinalIgnoreCase); // empty item: not answered
    }

    [Fact]
    public async Task K_A15_next_month_searches_that_range()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Ngày 15 đến 17 tháng sau còn xe 7 chỗ không?");
        Assert.Equal("search", r.Kind);
        Assert.Contains("15/04 08:00 → 17/04 08:00", r.Reply);
        Assert.All(r.Vehicles, v => Assert.Equal(7, v.Seats));
    }

    // ---------- K-B: app FAQ ----------

    [Theory]
    [InlineData("Quy trình thuê xe như thế nào?", "chờ điều phối xác nhận")]
    [InlineData("Tôi phải đặt cọc trước bao nhiêu?", "50% tổng tiền đơn")]
    [InlineData("Có thanh toán bằng chuyển khoản hoặc tiền mặt được không?", "4 phương thức")]
    [InlineData("Sau khi đặt xe thì bao lâu được xác nhận?", "chờ điều phối xác nhận")]
    [InlineData("Hợp đồng thuê xe được ký online hay trực tiếp?", "hợp đồng điện tử")]
    [InlineData("Tôi muốn thuê xe dài hạn theo tháng, bên bạn có hỗ trợ không?", "Chưa có giá thuê theo tháng")]
    [InlineData("Giá thuê cuối tuần có khác ngày thường không?", "không phân biệt ngày thường hay cuối tuần")]
    [InlineData("Tôi muốn thuê hai xe cùng lúc, bên bạn có đủ xe không?", "Mỗi đơn đặt 1 xe")]
    [InlineData("Nếu hủy đơn thì hoàn cọc thế nào?", "hoàn thủ công")]
    public async Task K_B_app_questions_are_answered_from_the_knowledge_file(string question, string expected)
    {
        using var iso = Setup();
        var r = await Ask(iso, question);
        Assert.Equal("faq", r.Kind);
        Assert.Contains(expected, r.Reply);
        Assert.DoesNotContain("30%", r.Reply);
    }

    // ---------- K-C: waiting for the company's rules → "chưa có thông tin", never invented ----------

    [Theory]
    [InlineData("Giá thuê xe đã bao gồm phí giao xe chưa?")]
    [InlineData("Có giao xe tận nhà hoặc sân bay không?")]
    [InlineData("Nếu trả xe muộn 2 tiếng thì tính thêm bao nhiêu tiền?")]
    [InlineData("Xe có bảo hiểm không?")]
    [InlineData("Nếu va quẹt thì sao?")]
    [InlineData("Xe thủng lốp giữa đường thì làm gì?")]
    [InlineData("Khi trả xe thì tiền cọc được hoàn lại trong bao lâu?")]
    [InlineData("Tôi có thể nhận xe ở TP.HCM và trả ở địa điểm khác không?")]
    [InlineData("Tôi muốn nhận xe lúc 6 giờ sáng, có được không?")]
    [InlineData("Đổi ngày thuê có phải đặt lại không?")]
    [InlineData("Tôi muốn gia hạn thêm 1 ngày")]
    [InlineData("Xe này có đang được bảo dưỡng không?")]
    [InlineData("Tôi cần bằng lái hạng B để thuê xe 7 chỗ tự lái phải không?")]
    [InlineData("Khách nước ngoài có thuê xe tự lái được không?")]
    [InlineData("Tôi cần chuẩn bị giấy tờ gì để thuê xe?")]
    [InlineData("Thuê xe có tài xế thì tài xế có chờ suốt chuyến đi không?")]
    [InlineData("Tiền ăn, nghỉ của tài xế do ai chi trả?")]
    [InlineData("Hotline bao nhiêu?")]
    [InlineData("Giờ làm việc của bên bạn?")]
    public async Task K_C_rules_not_written_yet_get_no_information(string question)
    {
        using var iso = Setup();
        var r = await Ask(iso, question);
        Assert.Equal("unknown", r.Kind);
        Assert.StartsWith(ChatbotService.Unknown, r.Reply);
        Assert.Empty(r.Vehicles);
    }

    // ---------- helpers ----------

    private static PricingQuote Quote(IsolatedCarRentalDb iso, int typeId, string mode, int days)
    {
        var start = Now.Date.AddDays(1) + ChatQueryParser.DefaultTime;
        return new PricingService().CalculateQuote(iso.Db.VehicleTypes.Find(typeId)!, mode, start, start.AddDays(days), null);
    }

    private static string Money(decimal amount) => amount.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    private static Task<ChatbotAskResponse> Ask(IsolatedCarRentalDb iso, string message)
        => new ChatbotService(iso.Db, new ScheduleConflictService(iso.Db), new PricingService(), Faq).AskAsync(message, Now);

    private static IsolatedCarRentalDb Setup()
    {
        var iso = new IsolatedCarRentalDb();
        foreach (var id in new[] { 1, 2, 3, 4, 5 })
        {
            var v = iso.Db.Vehicles.Find(id)!;
            v.Status = VehicleStatuses.Available;
            iso.SetLastCompletedMaintenance(id, DateTime.UtcNow.AddDays(-10), v.CurrentKm - 100);
        }
        foreach (var b in iso.Db.Bookings) { b.StartDate = new DateTime(2026, 1, 1); b.EndDate = new DateTime(2026, 1, 2); }
        iso.Db.SaveChanges();
        return iso;
    }

    private static string KnowledgePath()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "Backend", "Backend.csproj")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(dir!, "Backend", "Data", FaqKnowledgeBase.FileName);
    }
}
