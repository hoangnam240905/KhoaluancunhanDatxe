using System.Text.Json;
using Backend.Data;
using Backend.DTOs.Chatbot;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Chatbot;

/// <summary>
/// Chat history of a logged-in customer, so the widget shows earlier questions and answers again on any
/// page or device. Guests are not stored. Only the latest <see cref="MaxStored"/> lines are kept.
/// </summary>
public class ChatbotHistoryService(CarRentalDbContext db)
{
    public const int MaxStored = 200;
    public const int DefaultTake = 60;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<int> SaveAsync(int customerId, string question, ChatbotAskResponse answer)
        => SqliteWriteLock.ExecuteAsync(db, async () =>
        {
            var now = DateTime.UtcNow;
            db.ChatbotMessages.Add(new ChatbotMessage
            {
                CustomerId = customerId, FromCustomer = true, Text = Clip(question), CreatedAt = now
            });
            db.ChatbotMessages.Add(new ChatbotMessage
            {
                CustomerId = customerId, FromCustomer = false, Text = Clip(answer.Reply),
                AnswerJson = JsonSerializer.Serialize(answer, Json), CreatedAt = now.AddTicks(1)
            });
            await db.SaveChangesAsync();

            var old = await db.ChatbotMessages
                .Where(m => m.CustomerId == customerId)
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.ChatbotMessageId)
                .Skip(MaxStored)
                .ToListAsync();
            if (old.Count > 0)
            {
                db.ChatbotMessages.RemoveRange(old);
                await db.SaveChangesAsync();
            }
            return 2;
        });

    /// <summary>Latest messages, oldest first.</summary>
    public async Task<IReadOnlyList<ChatbotHistoryItem>> GetAsync(int customerId, int take = DefaultTake)
    {
        var rows = await db.ChatbotMessages.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.ChatbotMessageId)
            .Take(Math.Clamp(take, 1, MaxStored))
            .ToListAsync();
        rows.Reverse();
        return rows.Select(m => new ChatbotHistoryItem(
            m.FromCustomer, m.Text, m.CreatedAt,
            m.AnswerJson is null ? null : TryRead(m.AnswerJson))).ToList();
    }

    public Task<int> ClearAsync(int customerId)
        => SqliteWriteLock.ExecuteAsync(db, () =>
            db.ChatbotMessages.Where(m => m.CustomerId == customerId).ExecuteDeleteAsync());

    private static ChatbotAskResponse? TryRead(string json)
    {
        try { return JsonSerializer.Deserialize<ChatbotAskResponse>(json, Json); }
        catch (JsonException) { return null; }
    }

    private static string Clip(string text) => text.Length <= 2000 ? text : text[..2000];
}
