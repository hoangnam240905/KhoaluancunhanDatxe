using System.Text.Json;

namespace Backend.Services.Chatbot;

public sealed record FaqLink(string Text, string Url);

public sealed record FaqItem(string Id, string Category, IReadOnlyList<string> Keywords, string Answer, IReadOnlyList<FaqLink> Links);

/// <summary>
/// Written FAQ about using the app (plan1.md 1.5), loaded from Data/ChatbotKnowledge.json.
/// The chatbot only ever answers with text from this file — it never makes an answer up.
/// Items with an empty answer are treated as missing (filled later from the rental company's rules).
/// </summary>
public sealed class FaqKnowledgeBase
{
    public const string FileName = "ChatbotKnowledge.json";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<FaqItem> Items { get; }
    public string? ContactItemId { get; }

    public FaqKnowledgeBase(IReadOnlyList<FaqItem> items, string? contactItemId = null)
    {
        Items = items
            .Select(i => i with { Keywords = i.Keywords.Select(ChatQueryParser.Normalize).Where(k => k.Length > 0).ToList() })
            .ToList();
        ContactItemId = contactItemId;
    }

    public static FaqKnowledgeBase Load(string path)
    {
        var file = JsonSerializer.Deserialize<KnowledgeFile>(File.ReadAllText(path), Json)
                   ?? throw new InvalidOperationException($"{FileName} is empty.");
        return new FaqKnowledgeBase(file.Items ?? [], file.ContactFallback);
    }

    public static FaqKnowledgeBase LoadDefault()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", FileName);
        return File.Exists(path) ? Load(path) : new FaqKnowledgeBase([]);
    }

    /// <summary>
    /// Best item by number of keyword phrases found in the normalized message (longer phrases weigh more
    /// so "huy don" beats "don"). Only items that have an answer can win.
    /// </summary>
    public FaqItem? Match(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage)) return null;
        var padded = $" {normalizedMessage} ";
        FaqItem? best = null;
        var bestScore = 0;
        foreach (var item in Items.Where(i => !string.IsNullOrWhiteSpace(i.Answer)))
        {
            var score = item.Keywords.Where(k => padded.Contains($" {k} ") || (k.Contains(' ') && padded.Contains(k)))
                .Sum(k => k.Split(' ').Length);
            if (score > bestScore)
            {
                best = item;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>The contact item when it has been filled in; null otherwise.</summary>
    public FaqItem? Contact
        => ContactItemId is null ? null
            : Items.FirstOrDefault(i => i.Id == ContactItemId && !string.IsNullOrWhiteSpace(i.Answer));

    private sealed record KnowledgeFile(string? ContactFallback, List<FaqItem>? Items);
}
