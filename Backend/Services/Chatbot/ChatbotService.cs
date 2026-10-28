using System.Globalization;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Chatbot;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Chatbot;

/// <summary>
/// One question → one short answer (plan1.md 1.1). Order: vehicle search first (so "xe 7 chỗ…" never
/// falls into the FAQ), then the written FAQ, else an honest "no information".
/// Which vehicle is free is decided ONLY by ScheduleConflictService.FindAvailableVehiclesAsync; prices
/// ONLY by PricingService.CalculateQuote (same as the booking form).
/// </summary>
public class ChatbotService(
    CarRentalDbContext db,
    ScheduleConflictService schedule,
    PricingService pricing,
    FaqKnowledgeBase faq)
{
    public const int MaxVehicles = 5;
    public const int MaxMessageLength = 500;
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public const string AskDates = "Bạn muốn nhận và trả xe ngày nào? (ví dụ: từ ngày 22 đến ngày 25)";
    public const string AskSeats = "Bạn cần xe mấy chỗ? (ví dụ: 4 chỗ hoặc 7 chỗ)";
    public const string AskPeople = "Bạn đi mấy người hoặc cần xe mấy chỗ? (ví dụ: 4 người, hoặc xe 7 chỗ)";
    public const string AskPeopleAndDates = "Bạn đi mấy người và muốn nhận – trả xe ngày nào? (ví dụ: 4 người, từ ngày 22 đến 25)";
    public const string AskConcreteDate = "Bạn cho mình ngày nhận và trả xe cụ thể (ví dụ: từ ngày 22 đến 25) để kiểm tra xe còn trống nhé.";
    public const string PastDate = "Ngày nhận xe đã qua, bạn chọn ngày từ hôm nay trở đi nhé.";
    public const string Unknown = "Mình chưa có thông tin về câu này.";
    public const string Examples = "Bạn có thể hỏi tìm xe, ví dụ: \"Từ ngày 22 đến 25 còn xe 7 chỗ nào dưới 4 triệu?\", hoặc hỏi cách đặt xe, đặt cọc, hủy đơn.";

    public async Task<ChatbotAskResponse> AskAsync(string? message, DateTime? nowVn = null)
    {
        var now = nowVn ?? VietnamTime.Now;
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
            return Reply("ask", Examples);
        if (text.Length > MaxMessageLength)
            text = text[..MaxMessageLength];

        var query = ChatQueryParser.Parse(text, now);
        var normalized = ChatQueryParser.Normalize(text);
        // "Cần bằng lái hạng B để thuê xe 7 chỗ tự lái?" is a question about a rule, not a search: a seat
        // count alone (no dates, people, length, budget or "còn xe trống") lets a rule topic answer.
        if (query.IsSearch && IsSeatMentionOnly(query, normalized) && faq.Best(normalized) is { RuleTopic: true } rule)
            return string.IsNullOrWhiteSpace(rule.Answer) ? NoInformation() : Faq(rule);
        if (query.IsSearch)
            return await SearchAsync(query, now);

        var item = faq.Match(normalized);
        if (item is not null)
            return Faq(item);
        return NoInformation();
    }

    private static bool IsSeatMentionOnly(ChatQuery q, string normalized)
        => q.Start is null && q.People is null && q.Days is null && q.MaxPrice is null
           && !q.CompareModes && !q.Cheapest && !q.SuvOnly && !q.RelativeDate
           && !normalized.Contains("trong") && !normalized.Contains("con xe") && !normalized.Contains("co xe nao");

    private static ChatbotAskResponse Faq(FaqItem item)
        => new("faq", item.Answer, [], item.Links.Select(l => new ChatbotLink(l.Text, l.Url)).ToList());

    private ChatbotAskResponse NoInformation()
    {
        var contact = faq.Contact;
        return new ChatbotAskResponse(
            "unknown",
            contact is null ? $"{Unknown} {Examples}" : $"{Unknown} {contact.Answer}",
            [],
            contact?.Links.Select(l => new ChatbotLink(l.Text, l.Url)).ToList() ?? []);
    }

    private async Task<ChatbotAskResponse> SearchAsync(ChatQuery query, DateTime now)
    {
        var active = await db.VehicleTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SeatCapacity).ThenBy(t => t.TypeId)
            .ToListAsync();
        var (types, note) = PickTypes(query, active);
        var lead = note is null ? "" : note + " ";

        // "chưa biết 4 chỗ hay 7 chỗ, tư vấn giúp" → ask how many people; never pick a car for them.
        if (query.WantsAdvice && query.People is null && query.Start is null)
            return Reply("ask", AskPeopleAndDates);

        if (query.Start is not DateTime start || query.End is not DateTime end)
        {
            if (types.Count == 0)
            {
                if (note is not null) return Reply("ask", note);
                return Reply("ask", query.Days is not null || query.CompareModes ? AskPeople : AskPeopleAndDates);
            }
            if (query.Days is int days)
                return Estimate(query, types, days, lead, now);
            return Reply("ask", lead + (query.RelativeDate ? AskConcreteDate : AskDates));
        }
        if (BookingDateRules.ValidateNewRental(start, end) is not null)
            return Reply("ask", start.Date < now.Date ? PastDate : AskDates);
        if (types.Count == 0)
        {
            if (note is not null) return Reply("ask", note);
            if (query.Seats.Count == 0) return Reply("ask", AskSeats);
            var existing = active.Select(t => t.SeatCapacity).Distinct();
            return Reply("search",
                $"Hiện không có loại xe {Seats(query.Seats)}. Các loại đang có: {string.Join(", ", existing.Select(s => $"{s} chỗ"))}.");
        }

        var found = new List<ChatbotVehicle>();
        var quotedDays = 0;
        foreach (var type in types)
        {
            var quote = pricing.CalculateQuote(type, query.RentalMode, start, end, null);
            quotedDays = quote.QuotedDays;
            if (MaxFor(query, quote.QuotedDays) is decimal max && quote.TotalAmount > max)
                continue;
            var free = await schedule.FindAvailableVehiclesAsync(type.TypeId, start, end);
            found.AddRange(free.Select(v => new ChatbotVehicle(
                v.VehicleId,
                $"{v.Brand} {v.Model}".Trim(),
                type.TypeName,
                type.SeatCapacity,
                v.LicensePlate,
                quote.QuotedDays,
                quote.TotalAmount)));
        }

        var seatList = types.Select(t => t.SeatCapacity).Distinct().ToList();
        var echo = new ChatbotSearchEcho(start, end, query.RentalMode, seatList, MaxFor(query, quotedDays));
        var when = $"{start:dd/MM HH:mm} → {end:dd/MM HH:mm}";
        var mode = query.RentalMode == RentalModes.SelfDrive ? "tự lái" : "có tài xế, chưa gồm cước km";
        var compare = query.CompareModes ? $" So sánh {types[0].TypeName}: {Compare(types[0], start, end)}." : "";
        if (found.Count == 0)
        {
            var hint = query.MaxPrice is not null
                ? "Bạn có muốn tăng ngân sách hoặc đổi ngày không?"
                : seatList.Count == 1 ? $"Bạn có muốn xem loại xe khác số chỗ không?" : "Bạn có muốn đổi ngày không?";
            return new ChatbotAskResponse("search",
                $"{lead}Không còn xe {Seats(seatList)} trống phù hợp ({when}{Budget(query, quotedDays)}). {hint}{compare}",
                [], [new ChatbotLink("Tìm xe", "/Search")], echo);
        }

        var list = found
            .OrderBy(v => v.TotalAmount).ThenBy(v => v.VehicleId)
            .Take(MaxVehicles)
            .ToList();
        var more = found.Count > list.Count ? $" Hiện {list.Count} xe rẻ nhất trong {found.Count} xe." : "";
        return new ChatbotAskResponse("search",
            $"{lead}Có {found.Count} xe phù hợp ({when}, {mode}{Budget(query, quotedDays)}).{more}{compare}",
            list, [], echo);
    }

    /// <summary>
    /// Vehicle types the question is about. "N chỗ" = exactly N seats (T01/T02); "N người" = the smallest
    /// type with at least N seats (R1); "gầm cao / SUV" = types named SUV (R4). Note = what the customer
    /// should know (too few seats for the group, no type large enough).
    /// </summary>
    private static (List<VehicleType> Types, string? Note) PickTypes(ChatQuery query, List<VehicleType> active)
    {
        var pool = query.SuvOnly
            ? active.Where(t => t.TypeName.Contains("SUV", StringComparison.OrdinalIgnoreCase)).ToList()
            : active;
        if (query.Seats.Count > 0)
        {
            var types = pool.Where(t => query.Seats.Contains(t.SeatCapacity)).ToList();
            var small = query.People is int p ? query.Seats.Where(s => s < p).Distinct().ToList() : [];
            var note = small.Count == 0 ? null : $"Xe {Seats(small)} không đủ chỗ cho {query.People} người.";
            return (types, note);
        }
        if (query.People is int people)
        {
            var fit = pool.Where(t => t.SeatCapacity >= people).ToList();
            if (fit.Count == 0)
                return ([], $"Chưa có loại xe nào đủ {people} chỗ{(query.SuvOnly ? " trong nhóm SUV" : "")}. Đoàn đông cần thuê nhiều xe — mỗi đơn đặt 1 xe.");
            var seats = fit.Min(t => t.SeatCapacity);
            return (fit.Where(t => t.SeatCapacity == seats).ToList(), null);
        }
        if (query.SuvOnly)
            return pool.Count == 0 ? ([], "Hiện không có loại xe SUV.") : (pool, null);
        return ([], null);
    }

    /// <summary>
    /// R5: a length without dates → estimated price for that many days (same CalculateQuote as the booking
    /// form, start date only fixes the day count), then ask for dates to check which cars are free.
    /// </summary>
    private ChatbotAskResponse Estimate(ChatQuery query, List<VehicleType> types, int days, string lead, DateTime now)
    {
        var start = now.Date.AddDays(1) + ChatQueryParser.DefaultTime;
        var end = start.AddDays(Math.Max(1, days));
        var parts = new List<string>();
        foreach (var type in types.Take(3))
        {
            if (query.CompareModes)
            {
                parts.Add($"{type.TypeName}: {Compare(type, start, end)}");
                continue;
            }
            var q = pricing.CalculateQuote(type, query.RentalMode, start, end, null);
            var detail = query.RentalMode == RentalModes.SelfDrive
                ? $"tự lái, miễn phí {q.QuotedSelfDriveIncludedKmPerDay:#,0} km/ngày, vượt tính {Money(q.QuotedSelfDriveExtraKmPrice)}/km"
                : "có tài xế, chưa gồm cước km";
            var budget = MaxFor(query, q.QuotedDays) is decimal max
                ? (q.TotalAmount <= max ? ", trong ngân sách" : ", vượt ngân sách")
                : "";
            parts.Add($"{type.TypeName}: {Money(q.TotalAmount)} ({detail}; cọc 50%: {Money(q.DepositAmount)}{budget})");
        }
        var budgetText = query.MaxPrice is decimal m && query.BudgetPerDay
            ? $" Ngân sách {Money(m)}/ngày × {days} ngày = {Money(m * days)}."
            : "";
        var ask = query.RelativeDate ? AskConcreteDate : "Bạn muốn nhận xe ngày nào để mình kiểm tra xe còn trống?";
        return Reply("ask", $"{lead}Giá ước tính {days} ngày — {string.Join("; ", parts)}.{budgetText} {ask}");
    }

    /// <summary>Both rental modes for one type and period (customer asked which is cheaper).</summary>
    private string Compare(VehicleType type, DateTime start, DateTime end)
    {
        var self = pricing.CalculateQuote(type, RentalModes.SelfDrive, start, end, null);
        var driver = pricing.CalculateQuote(type, RentalModes.WithDriver, start, end, null);
        var cheaper = self.TotalAmount <= driver.TotalAmount ? "tự lái rẻ hơn" : "có tài xế rẻ hơn";
        return $"tự lái {Money(self.TotalAmount)}, có tài xế {Money(driver.TotalAmount)} (chưa gồm cước km) → {cheaper} {Money(Math.Abs(self.TotalAmount - driver.TotalAmount))}";
    }

    /// <summary>R3: a daily budget is compared with the trip total as budget × rental days.</summary>
    private static decimal? MaxFor(ChatQuery query, int days)
        => query.MaxPrice is decimal m ? (query.BudgetPerDay ? m * Math.Max(1, days) : m) : null;

    private static ChatbotAskResponse Reply(string kind, string text) => new(kind, text, [], []);

    private static string Seats(IReadOnlyList<int> seats) => string.Join(" hoặc ", seats.Select(s => $"{s} chỗ"));

    private static string Money(decimal amount) => $"{amount.ToString("#,0", Vi)} đ";

    private static string Budget(ChatQuery query, int days)
        => query.MaxPrice is not decimal m ? ""
            : query.BudgetPerDay ? $", ngân sách {Money(m)}/ngày × {days} ngày = {Money(m * days)}"
            : $", tổng tiền ≤ {Money(m)}";
}