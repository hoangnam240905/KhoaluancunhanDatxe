using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Backend.Constants;

namespace Backend.Services.Chatbot;

/// <summary>What the parser understood from one customer message (null = not mentioned).</summary>
public sealed record ChatQuery(
    bool IsSearch,
    DateTime? Start,
    DateTime? End,
    bool SingleDay,
    IReadOnlyList<int> Seats,
    decimal? MaxPrice,
    string RentalMode,
    bool ModeMentioned,
    // plan1.md 1.7 — R1 "N người", R3 "X triệu/ngày", R4 SUV / gầm cao, R5 "N ngày" without dates.
    int? People = null,
    bool BudgetPerDay = false,
    bool SuvOnly = false,
    int? Days = null,
    // "rẻ hơn / so sánh" with both modes named; "tiết kiệm nhất"; "tư vấn / nên chọn";
    // a relative date ("cuối tuần", "thứ Bảy", "tối nay") that v1 does not turn into a date.
    bool CompareModes = false,
    bool Cheapest = false,
    bool WantsAdvice = false,
    bool RelativeDate = false);

/// <summary>
/// Rule-based reading of a Vietnamese question (plan1.md 1.1). No LLM: regex over the text after
/// lower-casing and removing diacritics, so "xe tu lai 4 cho" and "xe tự lái 4 chỗ" read the same.
/// </summary>
public static partial class ChatQueryParser
{
    /// <summary>Pickup / return time when the customer only gives dates.</summary>
    public static readonly TimeSpan DefaultTime = new(8, 0, 0);

    public static ChatQuery Parse(string? message, DateTime nowVn)
    {
        var text = Normalize(message);
        var seats = SeatsRegex().Matches(text)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Where(n => n is >= 2 and <= 50)
            .Distinct()
            .ToList();

        // R3: "1,5 triệu/ngày" is a daily budget. The "/ngày" part is cut so it is not read as a duration.
        var (maxPrice, perDay, work) = ParsePrice(text);
        // R5: "3 ngày", "2 ngày 1 đêm" = rental length, cut before reading dates ("ngày 1" is not a date here).
        var days = default(int?);
        var duration = DurationRegex().Match(work);
        if (duration.Success)
        {
            days = Number(duration.Groups["n"].Value);
            work = work.Remove(duration.Index, duration.Length).Insert(duration.Index, " ");
        }
        // R2: "tháng sau / tháng tới" → dates without a month are in next month.
        var dateBase = work.Contains("thang sau") || work.Contains("thang toi")
            ? new DateTime(nowVn.Year, nowVn.Month, 1).AddMonths(1)
            : nowVn;
        var (start, end, singleDay) = ParseDates(work, dateBase);

        var people = PeopleRegex().Match(text) is { Success: true } p ? int.Parse(p.Groups[1].Value, CultureInfo.InvariantCulture) : (int?)null;
        var suv = text.Contains("gam cao") || Regex.IsMatch(text, @"\bsuv\b");

        var selfDrive = text.Contains("tu lai");
        var withDriver = text.Contains("tai xe") || text.Contains("co lai");
        var mode = selfDrive && !withDriver ? RentalModes.SelfDrive : RentalModes.WithDriver;
        var compare = selfDrive && withDriver && CompareWords.Any(text.Contains);
        var cheapest = CheapestWords.Any(text.Contains);
        var advice = AdviceWords.Any(text.Contains);
        var relative = start is null && RelativeDateWords.Any(w => $" {text} ".Contains($" {w} "));

        // A length alone ("hủy trước 1 ngày", "gia hạn thêm 1 ngày") is not a search: it needs a rental word.
        var rentalLength = days is not null && !ChangeWords.Any(text.Contains) && (text.Contains("thue") || text.Contains(" xe") || people is not null);
        var isSearch = seats.Count > 0 || people is not null || start is not null || SearchWords.Any(text.Contains)
                       || suv || compare || cheapest || rentalLength || (maxPrice is not null && text.Contains("xe"));
        return new ChatQuery(isSearch, start, end, singleDay, seats, maxPrice, mode, selfDrive || withDriver,
            people, perDay, suv, days, compare, cheapest, advice, relative);
    }

    private static readonly string[] SearchWords =
    [
        "con xe", "xe trong", "xe nao trong", "xe nao con", "tim xe", "xe con trong", "co xe nao", "thue xe tu", "xe ranh",
        "nen chon xe", "nen thue xe", "xe nao phu hop"
    ];

    private static readonly string[] CompareWords = ["re hon", "dat hon", "so sanh", "loi hon", "tiet kiem hon"];
    private static readonly string[] CheapestWords = ["tiet kiem", "re nhat", "gia re"];
    private static readonly string[] AdviceWords = ["tu van", "nen chon", "nen thue", "chua biet"];
    private static readonly string[] ChangeWords = ["gia han", "them 1 ngay", "them mot ngay", "tra som", "doi ngay", "huy"];
    private static readonly string[] RelativeDateWords =
    [
        "cuoi tuan", "tuan sau", "tuan toi", "thu bay", "chu nhat", "thu hai", "thu ba", "thu tu", "thu nam", "thu sau",
        "ngay mai", "hom nay", "toi nay", "sang mai", "mai", "ngay kia"
    ];

    private static int Number(string word) => word switch
    {
        "mot" => 1,
        "hai" => 2,
        "ba" => 3,
        _ => int.Parse(word, CultureInfo.InvariantCulture)
    };

    /// <summary>Lower-case, strip Vietnamese diacritics (đ → d), collapse spaces.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return WhitespaceRegex().Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ");
    }

    /// <summary>Budget in VNĐ, whether it is per day, and the text with the "/ngày" suffix removed.</summary>
    private static (decimal? Price, bool PerDay, string Remaining) ParsePrice(string text)
    {
        var m = PriceRegex().Match(text);
        if (!m.Success) return (null, false, text);
        var number = decimal.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var unit = m.Groups[2].Value;
        var multiplier = unit is "trieu" or "tr" ? 1_000_000m : 1_000m; // nghin / ngan / k
        var price = Math.Round(number * multiplier, 0);

        var after = text[(m.Index + m.Length)..];
        var perDay = PerDayRegex().Match(after);
        return perDay.Success
            ? (price, true, text[..(m.Index + m.Length)] + " " + after[perDay.Length..])
            : (price, false, text);
    }

    private static (DateTime? Start, DateTime? End, bool SingleDay) ParseDates(string text, DateTime nowVn)
    {
        var range = RangeRegex().Match(text);
        if (range.Success)
        {
            var start = ToDate(range.Groups["d1"].Value, range.Groups["m1"].Value, range.Groups["y1"].Value, nowVn, null);
            if (start is null) return (null, null, false);
            var end = ToDate(range.Groups["d2"].Value, range.Groups["m2"].Value, range.Groups["y2"].Value, nowVn, start);
            if (end is null) return (null, null, false);
            return (start.Value.Date + DefaultTime, end.Value.Date + DefaultTime, false);
        }

        var single = SingleDateRegex().Match(text);
        if (single.Success)
        {
            var day = ToDate(single.Groups["d"].Value, single.Groups["m"].Value, single.Groups["y"].Value, nowVn, null);
            if (day is null) return (null, null, false);
            var start = day.Value.Date + DefaultTime;
            return (start, start.AddDays(1), true);
        }
        return (null, null, false);
    }

    /// <summary>
    /// Day only → this month, or next month when the day has already passed.
    /// End date earlier than start (no month given) → the month after the start.
    /// </summary>
    private static DateTime? ToDate(string dayText, string monthText, string yearText, DateTime nowVn, DateTime? after)
    {
        if (!int.TryParse(dayText, out var day) || day is < 1 or > 31) return null;
        var hasMonth = int.TryParse(monthText, out var month);
        var year = int.TryParse(yearText, out var y) ? (y < 100 ? 2000 + y : y) : (after?.Year ?? nowVn.Year);
        if (!hasMonth) month = after?.Month ?? nowVn.Month;
        if (month is < 1 or > 12) return null;

        DateTime? Build(int yy, int mm) => day <= DateTime.DaysInMonth(yy, mm) ? new DateTime(yy, mm, day) : null;

        var date = Build(year, month);
        if (date is null) return null;
        if (!hasMonth)
        {
            var floor = after?.Date ?? nowVn.Date;
            if (date < floor)
            {
                var next = new DateTime(year, month, 1).AddMonths(1);
                date = Build(next.Year, next.Month);
            }
        }
        else if (!int.TryParse(yearText, out _) && after is null && date < nowVn.Date)
        {
            date = Build(year + 1, month);
        }
        return date;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(\d{1,2})\s*(?:cho|ghe)\b")]
    private static partial Regex SeatsRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(trieu|tr|nghin|ngan|k)\b")]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"^\s*(?:dong\s*)?(?:/\s*|\bmoi\s+|\bmot\s+|1\s+)ngay\b")]
    private static partial Regex PerDayRegex();

    [GeneratedRegex(@"\b(?<n>\d{1,2}|mot|hai|ba)\s*ngay\b(?:\s*(?:\d{1,2}|mot|hai|ba)\s*dem\b)?")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"\b(\d{1,3})\s*nguoi\b")]
    private static partial Regex PeopleRegex();

    [GeneratedRegex(@"(?:ngay\s*)?(?<d1>\d{1,2})(?:\s*[/\-.]\s*(?<m1>\d{1,2})(?:\s*[/\-.]\s*(?<y1>\d{2,4}))?)?\s*(?:den|toi|->|~|-)\s*(?:ngay\s*)?(?<d2>\d{1,2})(?:\s*[/\-.]\s*(?<m2>\d{1,2})(?:\s*[/\-.]\s*(?<y2>\d{2,4}))?)?(?!\s*(?:cho|ghe|trieu|tr|nghin|ngan|k)\b)")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"\bngay\s*(?<d>\d{1,2})(?:\s*[/\-.]\s*(?<m>\d{1,2})(?:\s*[/\-.]\s*(?<y>\d{2,4}))?)?(?!\s*(?:cho|ghe|trieu|tr|nghin|ngan|k)\b)")]
    private static partial Regex SingleDateRegex();
}
