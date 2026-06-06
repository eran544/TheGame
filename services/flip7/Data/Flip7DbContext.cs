using Microsoft.EntityFrameworkCore;

namespace Flip7Server.Data;

/// <summary>
/// Owns the Flip7DB. Empty in Phase 1 — the Flip 7 domain (rounds, player
/// lines, modifiers, action cards, scores) is added in Phase 2. Users are
/// referenced only by Guid; the canonical identity lives in the Auth service.
/// </summary>
public class Flip7DbContext : DbContext
{
    public Flip7DbContext(DbContextOptions<Flip7DbContext> options) : base(options) { }
}
