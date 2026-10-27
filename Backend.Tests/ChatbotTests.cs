using Backend.Constants;
using Backend.Entities;
using Backend.Services;
using Backend.Services.Chatbot;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Giai đoạn 1 — chatbot: rule-based parser, vehicle search via the real availability + pricing, written FAQ.</summary>
public class ChatbotTests
{
    // Seed fleet: 1 Vios, 2 Accent (4 chỗ, type 1); 3 Fortuner, 4 Everest (7 chỗ, type 2); 5 Solati (16 chỗ); 6 Transit (9 chỗ).
    private static readonly DateTime Now = new(2027, 3, 10, 10, 0, 0);
    private static readonly FaqKnowledgeBase Faq = FaqKnowledgeBase.Load(KnowledgePath());

    // ---------- parser ----------

    [Fact]
    public void Parser_reads_dates_seats_budget_and_mode_with_or_without_accents()
    {
        var a = ChatQueryParser.Parse("Từ ngày 22 đến ngày 25, còn xe 7 chỗ nào trống, giá thuê dưới 4 triệu không?", Now);
        Assert.True(a.IsSearch);
        Assert.Equal(new DateTime(2027, 3, 22, 8, 0, 0), a.Start);
        Assert.Equal(new DateTime(2027, 3, 25, 8, 0, 0), a.End);
        Assert.Equal([7], a.Seats);
        Assert.Equal(4_000_000m, a.MaxPrice);
        Assert.Equal(RentalModes.WithDriver, a.RentalMode);

        var b = ChatQueryParser.Parse("xe tu lai 4 cho hoac 7 cho tu 22 den 25 duoi 500k", Now);
        Assert.Equal([4, 7], b.Seats);
        Assert.Equal(RentalModes.SelfDrive, b.RentalMode);
        Assert.Equal(500_000m, b.MaxPrice);
        Assert.Equal(new DateTime(2027, 3, 22, 8, 0, 0), b.Start);
    }

    [Theory]
    [InlineData("tu 28 den 2", "2027-03-28", "2027-04-02")]   // end day < start day -> next month
    [InlineData("tu 5 den 8", "2027-04-05", "2027-04-08")]    // day already passed this month -> next month
    [InlineData("22/10 - 25/10", "2027-10-22", "2027-10-25")]
    [InlineData("tu 30/12/2027 den 2/1/2028", "2027-12-30", "2028-01-02")]
    public void Parser_resolves_month_rollover(string text, string start, string end)
    {
        var q = ChatQueryParser.Parse(text, Now);
        Assert.Equal(DateTime.Parse(start).Date, q.Start!.Value.Date);
        Assert.Equal(DateTime.Parse(end).Date, q.End!.Value.Date);
    }

    [Fact]
    public void Single_day_means_one_rental_day()
    {
        var q = ChatQueryParser.Parse("ngày 22 còn xe 4 chỗ không", Now);
        Assert.True(q.SingleDay);
        Assert.Equal(new DateTime(2027, 3, 22, 8, 0, 0), q.Start);
        Assert.Equal(new DateTime(2027, 3, 23, 8, 0, 0), q.End);
    }

    [Theory]
    [InlineData("4tr", 4_000_000)]
    [InlineData("3,5 triệu", 3_500_000)]
    [InlineData("800 nghìn", 800_000)]
    public void Parser_reads_money_units(string text, decimal expected)
        => Assert.Equal(expected, ChatQueryParser.Parse($"xe 4 chỗ từ 22 đến 25 dưới {text}", Now).MaxPrice);

    // ---------- search (T01–T07) ----------

    [Fact]
    public async Task T01_only_matching_seats_free_and_within_total_budget()
    {
        using var iso = Setup();
        var withBudget = await Ask(iso, "Từ ngày 22 đến ngày 25, còn xe 7 chỗ nào trống, giá thuê dưới 6 triệu không?");
        Assert.Equal("search", withBudget.Kind);
        Assert.NotEmpty(withBudget.Vehicles);
        Assert.All(withBudget.Vehicles, v => Assert.Equal(7, v.Seats));
        // 7 chỗ có tài xế, 3 ngày: (1.200.000 + 600.000) × 3 = 5.400.000 — same formula as the booking form.
        Assert.All(withBudget.Vehicles, v => Assert.Equal(5_400_000m, v.TotalAmount));

        var tooLow = await Ask(iso, "Từ ngày 22 đến ngày 25, còn xe 7 chỗ nào trống, giá thuê dưới 4 triệu không?");
        Assert.Empty(tooLow.Vehicles);
        Assert.Contains("Không còn xe 7 chỗ", tooLow.Reply);
    }

    [Fact]
    public async Task T02_two_seat_groups_are_both_returned()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Còn xe 4 chỗ hoặc 7 chỗ nào trống từ 22 đến 25 không?");
        Assert.Contains(r.Vehicles, v => v.Seats == 4);
        Assert.Contains(r.Vehicles, v => v.Seats == 7);
        Assert.True(r.Vehicles.Count <= ChatbotService.MaxVehicles);
        Assert.Equal(r.Vehicles.OrderBy(v => v.TotalAmount).Select(v => v.VehicleId), r.Vehicles.Select(v => v.VehicleId));
    }

    [Fact]
    public async Task T03_unaccented_self_drive_uses_self_drive_price()
    {
        using var iso = Setup();
        var r = await Ask(iso, "xe tu lai 4 cho tu 22 den 25");
        Assert.NotEmpty(r.Vehicles);
        Assert.All(r.Vehicles, v => Assert.Equal(2_400_000m, v.TotalAmount)); // 800.000 × 3, no driver fee
        Assert.Equal(RentalModes.SelfDrive, r.Search!.RentalMode);
        Assert.Contains("tự lái", r.Reply);
    }

    [Fact]
    public async Task T04_busy_and_maintenance_vehicles_are_excluded_new_zero_km_vehicle_is_included()
    {
        using var iso = Setup();
        // Vehicle 3 has an overlapping rental; vehicle 4 is in maintenance.
        var busy = iso.AddDepositBooking(0, BookingStatuses.Assigned);
        busy.AssignedVehicleId = 3;
        busy.StartDate = new DateTime(2027, 3, 23, 8, 0, 0);
        busy.EndDate = new DateTime(2027, 3, 24, 8, 0, 0);
        OpenMaintenance(iso, 4);
        // Vehicle 2: brand-new car, 0 km, never serviced.
        var fresh = iso.Db.Vehicles.Find(2)!;
        fresh.CurrentKm = 0;
        fresh.CreatedAt = DateTime.UtcNow.AddDays(-5);
        iso.Db.SaveChanges();
        iso.ClearMaintenanceRecords(2);

        var r = await Ask(iso, "Còn xe 4 chỗ hoặc 7 chỗ nào trống từ 22 đến 25 không?");

        Assert.DoesNotContain(r.Vehicles, v => v.VehicleId is 3 or 4);
        Assert.Contains(r.Vehicles, v => v.VehicleId == 2);
    }

    [Fact]
    public async Task T05_no_match_is_short_and_returns_nothing_else()
    {
        using var iso = Setup();
        OpenMaintenance(iso, 3);
        OpenMaintenance(iso, 4);

        var r = await Ask(iso, "từ 22 đến 25 còn xe 7 chỗ không");

        Assert.Empty(r.Vehicles);
        Assert.StartsWith("Không còn xe 7 chỗ", r.Reply);
        Assert.Contains("xem loại xe khác", r.Reply);
    }

    [Theory]
    [InlineData("còn xe 7 chỗ nào trống?", ChatbotService.AskDates)]
    [InlineData("từ 22 đến 25 còn xe nào trống", ChatbotService.AskSeats)]
    public async Task T06_missing_detail_is_asked_in_one_sentence(string message, string expected)
    {
        using var iso = Setup();
        var r = await Ask(iso, message);
        Assert.Equal("ask", r.Kind);
        Assert.Equal(expected, r.Reply);
    }

    [Fact]
    public async Task T07_search_echo_carries_what_the_booking_link_needs()
    {
        using var iso = Setup();
        var r = await Ask(iso, "xe 4 chỗ có tài xế từ 22 đến 25");
        Assert.Equal(new DateTime(2027, 3, 22, 8, 0, 0), r.Search!.Start);
        Assert.Equal(new DateTime(2027, 3, 25, 8, 0, 0), r.Search.End);
        Assert.Equal(RentalModes.WithDriver, r.Search.RentalMode);
        Assert.All(r.Vehicles, v => Assert.True(v.VehicleId > 0));
    }

    // ---------- FAQ (T08–T11) ----------

    [Theory]
    [InlineData("huy don nhu the nao", "huy-don")]
    [InlineData("đặt cọc bao nhiêu", "tien-coc")]
    [InlineData("làm sao để thuê xe", "dat-xe")]
    [InlineData("quen mat khau thi lam gi", "mat-khau")]
    [InlineData("tự lái với có tài xế khác nhau thế nào", "hinh-thuc")]
    [InlineData("thanh toán bằng momo được không", "thanh-toan")]
    public async Task T08_each_category_answers_its_own_item(string message, string id)
    {
        using var iso = Setup();
        var r = await Ask(iso, message);
        Assert.Equal("faq", r.Kind);
        Assert.Equal(Faq.Items.Single(i => i.Id == id).Answer, r.Reply);
    }

    [Fact]
    public async Task T09_cancel_answer_follows_the_code_rule()
    {
        using var iso = Setup();
        var r = await Ask(iso, "Hủy đơn đã phân công được không?");
        Assert.Contains("24 giờ", r.Reply);
        Assert.DoesNotContain("30%", r.Reply);
    }

    [Theory]
    [InlineData("hotline bao nhiêu")]
    [InlineData("thời tiết hôm nay thế nào")]
    public async Task T10_unknown_or_empty_items_are_not_invented(string message)
    {
        using var iso = Setup();
        var r = await Ask(iso, message);
        Assert.Equal("unknown", r.Kind);
        Assert.StartsWith(ChatbotService.Unknown, r.Reply);
    }

    [Fact]
    public async Task T11_search_intent_wins_over_faq_words()
    {
        using var iso = Setup();
        var r = await Ask(iso, "hủy đơn cũ rồi, còn xe 7 chỗ từ 22 đến 25 không");
        Assert.Equal("search", r.Kind);
    }

    [Fact]
    public void Knowledge_file_has_no_unconfirmed_policy()
    {
        var all = string.Join(" ", Faq.Items.Select(i => i.Answer));
        Assert.DoesNotContain("30%", all);
        Assert.All(Faq.Items, i => Assert.All(i.Links, l => Assert.StartsWith("/", l.Url)));
    }

    // ---------- helpers ----------

    private static Task<Backend.DTOs.Chatbot.ChatbotAskResponse> Ask(IsolatedCarRentalDb iso, string message)
        => new ChatbotService(iso.Db, new ScheduleConflictService(iso.Db), new PricingService(), Faq).AskAsync(message, Now);

    /// <summary>Real maintenance flow: an open record puts the car out of service.</summary>
    private static void OpenMaintenance(IsolatedCarRentalDb iso, int vehicleId)
    {
        iso.Db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            VehicleId = vehicleId,
            MaintenanceType = "Periodic",
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.Vehicles.Find(vehicleId)!.Status = VehicleStatuses.Maintenance;
        iso.Db.SaveChanges();
    }

    private static IsolatedCarRentalDb Setup()
    {
        var iso = new IsolatedCarRentalDb();
        foreach (var id in new[] { 1, 2, 3, 4, 5 })
        {
            var v = iso.Db.Vehicles.Find(id)!;
            v.Status = VehicleStatuses.Available;
            iso.SetLastCompletedMaintenance(id, DateTime.UtcNow.AddDays(-10), v.CurrentKm - 100);
        }
        // Seed bookings must not overlap the searched dates.
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
