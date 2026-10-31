using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Chatbot;
using Backend.Services.Chatbot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Chatbot: answers are public vehicle info + prices only. A logged-in customer's questions and answers
/// are saved as their chat history (GET / DELETE history); guests are not stored.
/// </summary>
[ApiController]
[Route("api/chatbot")]
public class ChatbotController(ChatbotService chatbot, ChatbotHistoryService history) : ControllerBase
{
    // Anonymous here only: [AllowAnonymous] on the controller would also open the history actions.
    [AllowAnonymous]
    [HttpPost("ask")]
    public async Task<ActionResult<ChatbotAskResponse>> Ask(ChatbotAskRequest request)
    {
        var answer = await chatbot.AskAsync(request.Message);
        if (CustomerId() is { } customerId && !string.IsNullOrWhiteSpace(request.Message))
            await history.SaveAsync(customerId, request.Message.Trim(), answer);
        return Ok(answer);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ChatbotHistoryItem>>> History([FromQuery] int take = ChatbotHistoryService.DefaultTake)
        => Ok(await history.GetAsync(CustomerId()!.Value, take));

    [Authorize(Roles = RoleNames.Customer)]
    [HttpDelete("history")]
    public async Task<IActionResult> ClearHistory()
    {
        await history.ClearAsync(CustomerId()!.Value);
        return NoContent();
    }

    // JWT NameIdentifier == CustomerId for the Customer role (same as the other customer APIs).
    private int? CustomerId()
        => User.IsInRole(RoleNames.Customer) && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}