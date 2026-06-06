namespace GameCommon.Identity;

/// <summary>
/// Minimal identity contract shared across services. Lets game-agnostic
/// infrastructure (e.g. JWT issuance) work without depending on any concrete
/// per-service user entity.
/// </summary>
public interface IIdentityUser
{
    Guid Id { get; }
    string Username { get; }
    bool IsAdmin { get; }
}
