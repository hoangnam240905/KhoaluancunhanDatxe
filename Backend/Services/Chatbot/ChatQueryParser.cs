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
    bool ModeMentioned);

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
        var maxPrice = ParsePrice(text);
        var (start, end, singleDay) = ParseDates(text, nowVn);

        var selfDrive = text.Contains("tu lai");
        var withDriver = text.Contains("tai xe") || text.Contains("co lai");
        var mode = selfDrive && !withDriver ? RentalModes.SelfDrive : RentalModes.WithDriver;

        var isSearch = seats.Count > 0 || start is not null || SearchWords.Any(text.Contains);
        return new ChatQuery(isSearch, start, end, singleDay, seats, maxPrice, mode, selfDrive || withDriver);
    }

    private static readonly string[] SearchWords =
    [
        "con xe", "xe trong", "xe nao trong", "xe nao con", "tim xe", "xe con trong", "co xe nao", "thue xe tu", "xe ranh"
    ];

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

    private static decimal? ParsePrice(string text)
    {
        var m = PriceRegex().Match(text);
        if (!m.Success) return null;
        var number = decimal.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var unit = m.Groups[2].Value;
        var multiplier = unit is "trieu" or "tr" ? 1_000_000m : 1_000m; // nghin / ngan / k
        return Math.Round(number * multiplier, 0);
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

    [GeneratedRegex(@"(?:ngay\s*)?(?<d1>\d{1,2})(?:\s*[/\-.]\s*(?<m1>\d{1,2})(?:\s*[/\-.]\s*(?<y1>\d{2,4}))?)?\s*(?:den|toi|->|~|-)\s*(?:ngay\s*)?(?<d2>\d{1,2})(?:\s*[/\-.]\s*(?<m2>\d{1,2})(?:\s*[/\-.]\s*(?<y2>\d{2,4}))?)?(?!\s*(?:cho|ghe|trieu|tr|nghin|ngan|k)\b)")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"\bngay\s*(?<d>\d{1,2})(?:\s*[/\-.]\s*(?<m>\d{1,2})(?:\s*[/\-.]\s*(?<y>\d{2,4}))?)?(?!\s*(?:cho|ghe|trieu|tr|nghin|ngan|k)\b)")]
    private static partial Regex SingleDateRegex();
}
