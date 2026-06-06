namespace TheGameServer.DTOs.Game;

// ── Shared ─────────────────────────────────────────────────────────────────
public record PileTopsDto(int Ascending1, int Ascending2, int Descending1, int Descending2);
public record FinalScoreDto(int CardsRemaining, bool IsPerfectGame, string Rating);
public record PlayerInGameDto(Guid UserId, string Username, int HandCount, bool IsAI, bool IsCurrentTurn, bool IsDisconnected);
public record LastMovePlayDto(int Card, int PileSlot);
public record LastMoveDto(string PlayerUsername, IList<LastMovePlayDto> Plays);

public record GameStateDto(
    Guid SessionId,
    string GamePhase,
    bool IsExpertMode,
    PileTopsDto Piles,
    int DrawPileCount,
    int PlayedCardsCount,
    IList<int> Hand,
    int MinCardsThisTurn,
    FinalScoreDto? FinalScore,
    bool CanUndo,
    Guid? CurrentPlayerId = null,
    IList<PlayerInGameDto>? Players = null,
    IList<LastMoveDto>? RecentMoves = null);

public record TurnOutcomeDto(GameStateDto State, bool GameEnded, string? EndReason);

// ── Single-player ──────────────────────────────────────────────────────────
public record StartGameRequest(bool IsExpertMode = false);

// Slot: 0=Ascending1, 1=Ascending2, 2=Descending1, 3=Descending2
public record PlayCardDto(int Card, int Slot);
public record PlayTurnRequest(IList<PlayCardDto> Plays);

// ── Multiplayer ────────────────────────────────────────────────────────────
public record CreateMultiplayerGameRequest(int MaxPlayers, bool IsExpertMode = false);
public record LobbyPlayerDto(Guid UserId, string Username, int PlayerIndex, bool IsAI);
public record LobbyStateDto(
    Guid SessionId,
    string GamePhase,
    IList<LobbyPlayerDto> Players,
    int MaxPlayers,
    bool IsExpertMode,
    bool CanStart,
    Guid CreatedBy);
