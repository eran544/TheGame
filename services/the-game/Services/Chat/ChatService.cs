using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheGameServer.Data;
using TheGameServer.Models;

namespace TheGameServer.Services.Chat;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int ViolationThreshold = 5;

    public ChatService(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ChatSendResult> SendMessageAsync(Guid sessionId, Guid userId, string message)
    {
        var session = await _db.GameSessions
            .Include(s => s.Players)
            .SingleOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            return new ChatSendResult(true, "Session not found", 0, null);
        if (session.Players.All(p => p.UserId != userId))
            return new ChatSendResult(true, "You are not a member of this game", 0, null);

        var violationCount = await _db.ChatMessages
            .CountAsync(m => m.GameSessionId == sessionId && m.UserId == userId && !m.IsValidated);

        bool isBlocked;
        string? blockReason;

        if (violationCount >= ViolationThreshold)
        {
            isBlocked = true;
            blockReason = "Your chat has been restricted due to repeated rule violations.";
        }
        else
        {
            (isBlocked, blockReason) = await ValidateWithAiAsync(message);
        }

        var user = await _db.Users.SingleAsync(u => u.Id == userId);

        var chatMessage = new ChatMessage
        {
            GameSessionId = sessionId,
            UserId = userId,
            Message = message,
            IsValidated = !isBlocked,
            ValidationReason = blockReason,
        };
        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync();

        if (isBlocked)
            return new ChatSendResult(true, blockReason, violationCount + 1, null);

        var record = new ChatMessageRecord(chatMessage.Id, userId, user.Username, message, true, chatMessage.SentAt);
        return new ChatSendResult(false, null, violationCount, record);
    }

    public async Task<IList<ChatMessageRecord>> GetHistoryAsync(Guid sessionId)
    {
        return await _db.ChatMessages
            .Include(m => m.User)
            .Where(m => m.GameSessionId == sessionId && m.IsValidated)
            .OrderBy(m => m.SentAt)
            .Take(100)
            .Select(m => new ChatMessageRecord(m.Id, m.UserId, m.User.Username, m.Message, m.IsValidated, m.SentAt))
            .ToListAsync();
    }

    private async Task<(bool isBlocked, string? reason)> ValidateWithAiAsync(string message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AiService");
            var response = await client.PostAsJsonAsync("/validate-message", new { message });
            if (!response.IsSuccessStatusCode) return (false, null);

            var result = await response.Content.ReadFromJsonAsync<AiValidationResponse>(_jsonOptions);
            if (result is null) return (false, null);

            return (!result.IsAllowed, result.IsAllowed ? null : result.Reason);
        }
        catch
        {
            return (false, null); // fail open — allow the message if AI service is unreachable
        }
    }

    private record AiValidationResponse(bool IsAllowed, string? Reason);
}
