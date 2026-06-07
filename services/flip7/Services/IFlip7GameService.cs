using Flip7Server.DTOs;
using Flip7Server.Models;

namespace Flip7Server.Services;

/// <summary>
/// Orchestrates Flip 7 games over persistence: creating games, dealing rounds,
/// applying hit/stay, banking scores across rounds, and detecting game end.
/// Solo play needs no action-card targeting (the lone active player self-targets
/// automatically); multiplayer targeting is layered on in the hub.
/// </summary>
public interface IFlip7GameService
{
    Task<Flip7GameStateDto> CreateSoloAsync(Guid userId, string username, int? targetScore, CancellationToken ct = default);

    /// <summary>Creates a game with AI opponents. <paramref name="mode"/> is VsAi (starts immediately)
    /// or Online (waits in a lobby for humans to join and the creator to start).</summary>
    Task<Flip7GameStateDto> CreateGameAsync(Flip7GameMode mode, Guid creatorUserId, string creatorUsername,
        IReadOnlyList<Flip7AiSpec> aiPlayers, int? targetScore, CancellationToken ct = default);

    /// <summary>Adds a human to an Online game still in its lobby.</summary>
    Task<Flip7GameStateDto> JoinAsync(Guid gameId, Guid userId, string username, CancellationToken ct = default);

    /// <summary>Creator deals the first round of an Online game (leaving the lobby).</summary>
    Task<Flip7GameStateDto> StartAsync(Guid gameId, Guid creatorUserId, CancellationToken ct = default);

    Task<Flip7GameStateDto> HitAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    Task<Flip7GameStateDto> StayAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>Deals the next round after the current one has ended (game not yet over).</summary>
    Task<Flip7GameStateDto> NextRoundAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    Task<Flip7GameStateDto?> GetStateAsync(Guid gameId, Guid userId, CancellationToken ct = default);
}
