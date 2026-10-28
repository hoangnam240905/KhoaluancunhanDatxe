using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.DTOs.Auth;
using Backend.DTOs.Chatbot;
using Backend.Services.Chatbot;
using Xunit;

namespace Backend.Tests;

/// <summary>Chatbot history: a logged-in customer's chat is saved and shown again; guests are not stored.</summary>
public class ChatbotHistoryTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public ChatbotHistoryTests(IsolatedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Customer_questions_and_answers_come_back_in_order_and_can_be_cleared()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "customer2@gmail.com");
        await Send(client, HttpMethod.Delete, "/api/chatbot/history", token);

        await Ask(client, "Đặt cọc bao nhiêu?", token);
        await Ask(client, "Hủy đơn như thế nào?", token);

        var history = await History(client, token);
        Assert.Equal(4, history.Count);
        Assert.Equal([true, false, true, false], history.Select(h => h.FromCustomer));
        Assert.Equal("Đặt cọc bao nhiêu?", history[0].Text);
        Assert.Null(history[0].Answer);
        Assert.Equal("faq", history[1].Answer!.Kind);
        Assert.Contains("50%", history[1].Text);
        Assert.Contains("24 giờ", history[3].Answer!.Reply);

        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Delete, "/api/chatbot/history", token)).StatusCode);
        Assert.Empty(await History(client, token));
    }

    [Fact]
    public async Task Guests_are_not_stored_and_customers_only_see_their_own_chat()
    {
        var client = _factory.CreateClient();
        var mine = await LoginAsync(client, "customer2@gmail.com");
        await Send(client, HttpMethod.Delete, "/api/chatbot/history", mine);

        await Ask(client, "Tiền cọc bao nhiêu?", token: null); // guest
        Assert.Empty(await History(client, mine));

        var other = await LoginAsync(client, "customer1@gmail.com");
        await Ask(client, "Thanh toán bằng gì?", other);
        Assert.Empty(await History(client, mine));
        Assert.Contains(await History(client, other), h => h.Text == "Thanh toán bằng gì?");
    }

    [Fact]
    public async Task History_is_for_customers_only()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/chatbot/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/chatbot/history")).StatusCode);
        var dispatcher = await LoginAsync(client, "dispatcher@carrental.vn");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Get, "/api/chatbot/history", dispatcher)).StatusCode);
    }

    [Fact]
    public async Task Only_the_latest_messages_are_kept()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new ChatbotHistoryService(iso.Db);
        var answer = new ChatbotAskResponse("faq", "Trả lời", [], []);
        for (var i = 0; i < ChatbotHistoryService.MaxStored / 2 + 5; i++)
            await service.SaveAsync(3, $"Câu {i}", answer);

        Assert.Equal(ChatbotHistoryService.MaxStored, iso.Db.ChatbotMessages.Count(m => m.CustomerId == 3));
        var latest = await service.GetAsync(3, take: 2);
        Assert.Equal($"Câu {ChatbotHistoryService.MaxStored / 2 + 4}", latest[0].Text);
    }

    private static async Task Ask(HttpClient client, string message, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/ask") { Content = JsonContent.Create(new ChatbotAskRequest(message)) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    private static async Task<List<ChatbotHistoryItem>> History(HttpClient client, string token)
    {
        var response = await Send(client, HttpMethod.Get, "/api/chatbot/history", token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ChatbotHistoryItem>>())!;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
