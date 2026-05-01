namespace TheGameServer.DTOs.Game;

public record StartGameRequest(bool IsExpertMode = false);

// Slot: 0=Ascending1, 1=Ascending2, 2=Descending1, 3=Descending2
public record PlayCardDto(int Card, int Slot);

public record PlayTurnRequest(IList<PlayCardDto> Plays);

public record PileTopsDto(int Ascending1, int Ascending2, int Descending1, int Descending2);

public record FinalScoreDto(int CardsRemaining, bool IsPerfectGame, string Rating);

public record GameStateDto(
    Guid SessionId,
    string GamePhase,
    bool IsExpertMode,
    PileTopsDto Piles,
    int DrawPileCount,
    int PlayedCardsCount,
    IList<int> Hand,
    int MinCardsThisTurn,
    FinalScoreDto? FinalScore);

public record TurnOutcomeDto(GameStateDto State, bool GameEnded, string? EndReason);
