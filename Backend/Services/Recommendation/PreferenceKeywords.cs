using System.Globalization;
using System.Text;

namespace Backend.Services.Recommendation;

/// <summary>
/// V7's text channel (TF-IDF, 40% of the score) is fitted ONLY on the English car descriptions of the
/// dataset ("make … model … segment SUV fuel hybrid seats 7 … trunk … safety … fuel economy …").
/// Vietnamese notes share no words with that vocabulary, so this maps Vietnamese phrases (with or
/// without diacritics) onto those exact tokens. Simple phrase lookup — not machine translation.
/// </summary>
public static class PreferenceKeywords
{
    /// <summary>Phrase (lower-case, no diacritics) → tokens present in the car-description vocabulary.</summary>
    private static readonly (string Phrase, string Tokens)[] Map =
    [
        ("tiet kiem nhien lieu", "fuel economy"),
        ("tiet kiem xang", "fuel economy"),
        ("tiet kiem dau", "fuel economy"),
        ("it hao xang", "fuel economy"),
        ("an toan", "safety"),
        ("cop rong", "trunk"),
        ("cop xe", "trunk"),
        ("hanh ly", "trunk"),
        ("rong rai", "seats 7 seater"),
        ("7 cho", "seats 7 seater"),
        ("bay cho", "seats 7 seater"),
        ("nhieu nguoi", "seats 7 seater"),
        ("dong nguoi", "seats 7 seater"),
        ("5 cho", "seats 5"),
        ("4 cho", "seats 4"),
        ("gam cao", "suv"),
        ("duong deo", "suv"),
        ("leo deo", "suv"),
        ("di nui", "suv"),
        ("dia hinh", "suv"),
        ("duong xau", "suv"),
        ("di pho", "supermini mini"),
        ("noi thanh", "supermini mini"),
        ("trong thanh pho", "supermini mini"),
        ("de do xe", "supermini mini"),
        ("xe nho", "supermini mini"),
        ("nho gon", "supermini mini"),
        // "family" is spread evenly over Crossover / Sedan / Supermini in the data -> no segment token,
        // otherwise it overrides an explicit "7 chỗ" in the same note.
        ("gia dinh", "safety"),
        ("tre em", "safety"),
        ("tre nho", "safety"),
        ("con nho", "safety"),
        ("cong tac", "sedan"),
        ("sang trong", "sedan"),
        ("hybrid", "hybrid"),
        ("xe xanh", "hybrid"),
        ("than thien moi truong", "hybrid"),
        ("xe dien", "hybrid"),
        ("xang", "gas"),
        ("may dau", "gas"),
    ];

    /// <summary>Segment / fuel values from the Profile dropdowns → description tokens.</summary>
    public static string? VehicleTypeTokens(string? value) => Normalize(value).Trim() switch
    {
        "sedan" => "sedan",
        "suv" => "suv",
        "crossover" => "crossover",
        "van" or "mpv" or "limousine" or "7 seater" => "seats 7 seater",
        "mini" or "supermini" => "supermini mini",
        _ => null
    };

    public static string? FuelTokens(string? value) => Normalize(value).Trim() switch
    {
        "hybrid" or "electric" => "hybrid",
        "petrol" or "gasoline" or "diesel" or "gas" => "gas",
        _ => null
    };

    /// <summary>Seat tokens for a party size: catalog seats are 2 / 4 / 5 / 7.</summary>
    public static string? SeatTokens(int? people) => people switch
    {
        >= 5 => "seats 7 seater",
        >= 3 => "seats 5",
        _ => null
    };

    /// <summary>Distinct tokens for every known phrase found in a free-text note.</summary>
    public static IReadOnlyList<string> FromNote(string? note)
    {
        var text = $" {Normalize(note)} ";
        var found = new List<string>();
        foreach (var (phrase, tokens) in Map)
        {
            if (text.Contains($" {phrase} ", StringComparison.Ordinal) && !found.Contains(tokens))
                found.Add(tokens);
        }
        return found;
    }

    /// <summary>Lower-case, strip Vietnamese diacritics (đ → d), keep letters/digits separated by single spaces.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastSpace = true;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastSpace = false;
            }
            else if (!lastSpace)
            {
                sb.Append(' ');
                lastSpace = true;
            }
        }
        return sb.ToString().TrimEnd();
    }
}
