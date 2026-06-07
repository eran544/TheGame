using Flip7Server.DTOs;

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

    Task<Flip7GameStateDto> HitAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    Task<Flip7GameStateDto> StayAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>Deals the next round after the current one has ended (game not yet over).</summary>
    Task<Flip7GameStateDto> NextRoundAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    Task<Flip7GameStateDto?> GetStateAsync(Guid gameId, Guid userId, CancellationToken ct = default);
}
