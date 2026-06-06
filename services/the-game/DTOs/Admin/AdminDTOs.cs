namespace TheGameServer.DTOs.Admin;

public record AdminDashboardDto(
    int TotalUsers,
    int ActiveGames,
    int TotalCompletedGames,
    int TotalChatViolations);

public record AdminUserDto(
    Guid Id,
    string Username,
    bool IsAdmin,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int TotalGames,
    int PerfectGames);

public record AdminGamePlayerDto(
    Guid UserId,
    string Username,
    bool IsAI);

public record AdminGameDto(
    Guid SessionId,
    string HostUsername,
    int PlayerCount,
    int MaxPlayers,
    DateTime StartedAt,
    List<AdminGamePlayerDto> Players);

public record AdminCreateUserRequest(string Username, string Password, bool IsAdmin = false);

public record AdminResetPasswordRequest(string NewPassword);
