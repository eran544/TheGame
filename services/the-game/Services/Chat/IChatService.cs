namespace TheGameServer.Services.Chat;

public interface IChatService
{
    Task<ChatSendResult> SendMessageAsync(Guid sessionId, Guid userId, string message);
    Task<IList<ChatMessageRecord>> GetHistoryAsync(Guid sessionId);
}

public record ChatSendResult(bool IsBlocked, string? BlockReason, int ViolationCount, ChatMessageRecord? Message);
public record ChatMessageRecord(Guid Id, Guid UserId, string Username, string Message, bool IsValidated, DateTime SentAt);
