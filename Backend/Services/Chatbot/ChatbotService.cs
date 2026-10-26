using System.Globalization;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Chatbot;
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
        if (query.IsSearch)
            return await SearchAsync(query, now);

        var item = faq.Match(ChatQueryParser.Normalize(text));
        if (item is not null)
            return new ChatbotAskResponse("faq", item.Answer, [], item.Links.Select(l => new ChatbotLink(l.Text, l.Url)).ToList());

        var contact = faq.Contact;
        return new ChatbotAskResponse(
            "unknown",
            contact is null ? $"{Unknown} {Examples}" : $"{Unknown} {contact.Answer}",
            [],
            contact?.Links.Select(l => new ChatbotLink(l.Text, l.Url)).ToList() ?? []);
    }

    private async Task<ChatbotAskResponse> SearchAsync(ChatQuery query, DateTime now)
    {
        if (query.Start is not DateTime start || query.End is not DateTime end)
            return Reply("ask", AskDates);
        if (BookingDateRules.ValidateNewRental(start, end) is not null)
            return Reply("ask", start.Date < now.Date ? PastDate : AskDates);
        if (query.Seats.Count == 0)
            return Reply("ask", AskSeats);

        var types = await db.VehicleTypes.AsNoTracking()
            .Where(t => t.IsActive && query.Seats.Contains(t.SeatCapacity))
            .OrderBy(t => t.SeatCapacity).ThenBy(t => t.TypeId)
            .ToListAsync();
        if (types.Count == 0)
        {
            var existing = await db.VehicleTypes.AsNoTracking().Where(t => t.IsActive)
                .Select(t => t.SeatCapacity).Distinct().OrderBy(s => s).ToListAsync();
            return Reply("search",
                $"Hiện không có loại xe {Seats(query.Seats)}. Các loại đang có: {string.Join(", ", existing.Select(s => $"{s} chỗ"))}.");
        }

        var found = new List<ChatbotVehicle>();
        foreach (var type in types)
        {
            var quote = pricing.CalculateQuote(type, query.RentalMode, start, end, null);
            if (query.MaxPrice is decimal max && quote.TotalAmount > max)
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

        var echo = new ChatbotSearchEcho(start, end, query.RentalMode, query.Seats, query.MaxPrice);
        var when = $"{start:dd/MM HH:mm} → {end:dd/MM HH:mm}";
        var mode = query.RentalMode == RentalModes.SelfDrive ? "tự lái" : "có tài xế, chưa gồm cước km";
        if (found.Count == 0)
        {
            var hint = query.MaxPrice is not null
                ? "Bạn có muốn tăng ngân sách hoặc đổi ngày không?"
                : query.Seats.Count == 1 ? $"Bạn có muốn xem loại xe khác số chỗ không?" : "Bạn có muốn đổi ngày không?";
            return new ChatbotAskResponse("search",
                $"Không còn xe {Seats(query.Seats)} trống phù hợp ({when}{Budget(query.MaxPrice)}). {hint}",
                [], [new ChatbotLink("Tìm xe", "/Search")], echo);
        }

        var list = found
            .OrderBy(v => v.TotalAmount).ThenBy(v => v.VehicleId)
            .Take(MaxVehicles)
            .ToList();
        var more = found.Count > list.Count ? $" Hiện {list.Count} xe rẻ nhất trong {found.Count} xe." : "";
        return new ChatbotAskResponse("search",
            $"Có {found.Count} xe phù hợp ({when}, {mode}).{more}",
            list, [], echo);
    }

    private static ChatbotAskResponse Reply(string kind, string text) => new(kind, text, [], []);

    private static string Seats(IReadOnlyList<int> seats) => string.Join(" hoặc ", seats.Select(s => $"{s} chỗ"));

    private static string Budget(decimal? max)
        => max is decimal m ? $", tổng tiền ≤ {m.ToString("#,0", Vi)} đ" : "";
}
