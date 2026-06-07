namespace Flip7Server.Game;

/// <summary>
/// Default heuristic for action-card targeting in multiplayer, used as the
/// <see cref="TargetChooser"/> for both AI players and (for now) human draws.
/// The engine only consults a chooser when more than one candidate exists, so
/// solo / last-player self-targeting never reaches here.
/// </summary>
public static class Flip7TargetSelector
{
    public static Guid Choose(ActionKind action, Guid drawer, IReadOnlyList<Guid> candidates, Flip7Round round)
    {
        var opponents = candidates.Where(id => id != drawer).ToList();

        switch (action)
        {
            // Force the opponent most likely to bust (the most unique numbers held);
            // never flip-three yourself when an opponent is available.
            case ActionKind.FlipThree:
                return opponents.Count > 0
                    ? opponents
                        .OrderByDescending(id => round.Line(id).Numbers.Count)
                        .ThenBy(id => id)
                        .First()
                    : drawer;

            // Deny the strongest opponent's momentum — freeze whoever is closest to
            // Flip 7 (then highest banked value).
            case ActionKind.Freeze:
                return opponents.Count > 0
                    ? opponents
                        .OrderByDescending(id => round.Line(id).Numbers.Count)
                        .ThenByDescending(id => round.Line(id).Score)
                        .ThenBy(id => id)
                        .First()
                    : drawer;

            // The engine only calls us with eligible recipients (active, no card yet).
            case ActionKind.SecondChance:
                return opponents.Count > 0 ? opponents.OrderBy(id => id).First() : drawer;

            default:
                return candidates[0];
        }
    }
}
