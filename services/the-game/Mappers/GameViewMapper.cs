using TheGameServer.DTOs.Game;
using TheGameServer.Services.Game;

namespace TheGameServer.Mappers;

/// <summary>
/// Converts the service-layer <see cref="GameStateView"/> into the wire
/// <see cref="GameStateDto"/>. Every path that broadcasts game state — controller
/// turns, hub disconnects, and turn-timeout replacements — maps through here so all
/// "GameStateUpdated" payloads share one shape (notably the game's Rating as a
/// string, not the raw enum value).
/// </summary>
public static class GameViewMapper
{
    public static GameStateDto ToDto(GameStateView v) => new(
        v.SessionId,
        v.GamePhase,
        v.IsExpertMode,
        new PileTopsDto(v.Piles.Ascending1, v.Piles.Ascending2, v.Piles.Descending1, v.Piles.Descending2),
        v.DrawPileCount,
        v.PlayedCardsCount,
        v.Hand,
        v.MinCardsThisTurn,
        v.FinalScore is null ? null : new FinalScoreDto(
            v.FinalScore.CardsRemaining,
            v.FinalScore.IsPerfectGame,
            v.FinalScore.Rating.ToString()),
        v.CanUndo,
        v.CurrentPlayerId,
        v.Players?.Select(p => new PlayerInGameDto(p.UserId, p.Username, p.HandCount, p.IsAI, p.IsCurrentTurn, p.IsDisconnected)).ToList(),
        v.RecentMoves?.Select(m => new LastMoveDto(
            m.PlayerUsername,
            m.Plays.Select(p => new LastMovePlayDto(p.Card, p.PileSlot)).ToList())).ToList());
}
