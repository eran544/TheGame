namespace Flip7Server.Models;

public enum Flip7GameMode
{
    /// <summary>One human, no opponents — a score-chase to the target. Action cards self-target.</summary>
    Solo,

    /// <summary>One human plus AI opponents.</summary>
    VsAi,

    /// <summary>Multiple online humans (optionally with AI).</summary>
    Online,
}

public enum Flip7GameStatus
{
    Lobby,
    InProgress,
    Completed,
}

/// <summary>
/// A Flip 7 game across multiple rounds, played to <see cref="TargetScore"/>.
/// The active round's full state lives in <see cref="RoundStateJson"/> (a
/// deterministic snapshot of <c>Flip7Round</c>); it is null between rounds and
/// once the game completes. Identity lives in the Auth service — players carry a
/// denormalized <c>Username</c> rather than a local user projection.
/// </summary>
public class Flip7GameSession
{
    public Guid Id { get; set; }

    public Flip7GameMode Mode { get; set; }
    public Flip7GameStatus Status { get; set; } = Flip7GameStatus.Lobby;

    public int TargetScore { get; set; } = 200;
    public int RoundNumber { get; set; }

    /// <summary>Seat of the current round's dealer; rotates each round.</summary>
    public int DealerSeat { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The winning player's id (Flip7Player.Id) once completed.</summary>
    public Guid? WinnerId { get; set; }

    /// <summary>Serialized active <c>Flip7Round</c> snapshot; null between rounds / at game end.</summary>
    public string? RoundStateJson { get; set; }

    public List<Flip7Player> Players { get; set; } = new();
    public List<Flip7RoundResult> RoundResults { get; set; } = new();
}
