using Backend.DTOs.Chatbot;
using Backend.Services.Chatbot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Public chatbot: only public vehicle info + prices, no personal data.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/chatbot")]
public class ChatbotController(ChatbotService chatbot) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<ActionResult<ChatbotAskResponse>> Ask(ChatbotAskRequest request)
        => Ok(await chatbot.AskAsync(request.Message));
}
