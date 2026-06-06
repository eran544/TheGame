using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TheGameServer.DTOs.Chat;
using TheGameServer.Hubs;
using TheGameServer.Services.Chat;

namespace TheGameServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<GameHub> _hub;

    public ChatController(IChatService chatService, IHubContext<GameHub> hub)
    {
        _chatService = chatService;
        _hub = hub;
    }

    [HttpPost("{sessionId:guid}")]
    public async Task<IActionResult> SendMessage(Guid sessionId, [FromBody] SendChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty" });
        if (request.Message.Length > 500)
            return BadRequest(new { error = "Message cannot exceed 500 characters" });

        var result = await _chatService.SendMessageAsync(sessionId, GetUserId(), request.Message);

        if (result.IsBlocked)
            return Ok(new ChatSendResultDto(true, result.BlockReason, result.ViolationCount, null));

        var dto = MapMessage(result.Message!);
        await _hub.Clients.Group(GameHub.GroupName(sessionId.ToString()))
            .SendAsync("ChatMessageReceived", dto);

        return Ok(new ChatSendResultDto(false, null, result.ViolationCount, dto));
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetHistory(Guid sessionId)
    {
        var history = await _chatService.GetHistoryAsync(sessionId);
        return Ok(history.Select(MapMessage).ToList());
    }

    private static ChatMessageDto MapMessage(ChatMessageRecord m) => new(
        m.Id.ToString(),
        m.UserId.ToString(),
        m.Username,
        m.Message,
        m.IsValidated,
        m.SentAt);

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
