namespace TheGameServer.DTOs.Chat;

public record SendChatRequest(string Message);

public record ChatMessageDto(
    string Id,
    string UserId,
    string Username,
    string Message,
    bool IsValidated,
    DateTime SentAt);

public record ChatSendResultDto(
    bool IsBlocked,
    string? BlockReason,
    int ViolationCount,
    ChatMessageDto? Message);
