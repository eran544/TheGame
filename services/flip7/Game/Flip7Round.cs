namespace Flip7Server.Game;

/// <summary>Why a round ended.</summary>
public enum RoundEndReason
{
    None,
    AllInactive,
    Flip7,
}

/// <summary>
/// Chooses the target of an action card when a genuine choice exists. Only
/// consulted when more than one candidate is available — solo play and
/// last-player situations resolve automatically (the single candidate, i.e.
/// self), so the chooser is never called there.
/// </summary>
public delegate Guid TargetChooser(ActionKind action, Guid drawingPlayer, IReadOnlyList<Guid> candidates);

/// <summary>
/// A single Flip 7 round as a deterministic state machine: deal → hit/stay loop,
/// with full action-card resolution (Freeze, Flip Three with deferred nested
/// actions, Second Chance), bust detection, and the Flip 7 bonus. Pure and
/// DB-free — the service layer wraps it with persistence, the hub, and AI.
///
/// Cards are drawn from the front of the supplied deck list, so tests can lay
/// out an exact draw order. When the draw pile empties mid-round the discard
/// pile is reshuffled back in (cards held in front of players stay in place).
/// </summary>
public sealed class Flip7Round
{
    private readonly List<Guid> _turnOrder;
    private readonly Dictionary<Guid, PlayerLine> _lines;
    private readonly List<Flip7Card> _drawPile;
    private readonly List<Flip7Card> _discard = new();
    private readonly Random _random;

    private int _currentIndex = -1;
    private bool _dealt;

    public Flip7Round(IReadOnlyList<Guid> turnOrder, IEnumerable<Flip7Card> deck, Random? random = null)
    {
        if (turnOrder.Count == 0)
            throw new ArgumentException("A round needs at least one player.", nameof(turnOrder));

        _turnOrder = turnOrder.ToList();
        _lines = _turnOrder.ToDictionary(id => id, id => new PlayerLine { PlayerId = id });
        _drawPile = deck.ToList();
        _random = random ?? Random.Shared;
    }

    /// <summary>Captures the full round state for persistence.</summary>
    public Flip7RoundSnapshot Capture() => new()
    {
        TurnOrder = _turnOrder.ToList(),
        Lines = _turnOrder.Select(id =>
        {
            var l = _lines[id];
            return new PlayerLineSnapshot
            {
                PlayerId = id,
                Numbers = l.Numbers.ToList(),
                Modifiers = l.Modifiers.ToList(),
                HasSecondChance = l.HasSecondChance,
                Status = l.Status,
                AchievedFlip7 = l.AchievedFlip7,
            };
        }).ToList(),
        DrawPile = _drawPile.ToList(),
        Discard = _discard.ToList(),
        CurrentIndex = _currentIndex,
        Dealt = _dealt,
        RoundEnded = RoundEnded,
        EndReason = EndReason,
    };

    /// <summary>Rebuilds a round from a snapshot produced by <see cref="Capture"/>.</summary>
    public static Flip7Round Restore(Flip7RoundSnapshot s, Random? random = null)
    {
        var round = new Flip7Round(s.TurnOrder, s.DrawPile, random);
        foreach (var ls in s.Lines)
        {
            var line = round._lines[ls.PlayerId];
            line.Numbers.AddRange(ls.Numbers);
            line.Modifiers.AddRange(ls.Modifiers);
            line.HasSecondChance = ls.HasSecondChance;
            line.Status = ls.Status;
            line.AchievedFlip7 = ls.AchievedFlip7;
        }
        round._discard.AddRange(s.Discard);
        round._currentIndex = s.CurrentIndex;
        round._dealt = s.Dealt;
        round.RoundEnded = s.RoundEnded;
        round.EndReason = s.EndReason;
        return round;
    }

    public IReadOnlyList<Guid> TurnOrder => _turnOrder;
    public PlayerLine Line(Guid playerId) => _lines[playerId];
    public IReadOnlyDictionary<Guid, PlayerLine> Lines => _lines;
    public int DrawPileCount => _drawPile.Count;
    public int DiscardCount => _discard.Count;

    /// <summary>Total cards that can still be drawn this round (draw pile + the discard that will reshuffle in).</summary>
    public int DrawableCount => _drawPile.Count + _discard.Count;

    /// <summary>
    /// Number value → copies remaining in the drawable pool (draw pile + discard).
    /// This is the unseen pool a card-counter would reason over for bust odds.
    /// </summary>
    public IReadOnlyDictionary<int, int> DrawableNumberCounts()
    {
        var counts = new Dictionary<int, int>();
        foreach (var card in _drawPile.Concat(_discard))
            if (card.Kind == CardKind.Number)
                counts[card.Number!.Value] = counts.GetValueOrDefault(card.Number!.Value) + 1;
        return counts;
    }

    public bool RoundEnded { get; private set; }
    public RoundEndReason EndReason { get; private set; } = RoundEndReason.None;

    /// <summary>The player whose turn it is, or null once the round has ended.</summary>
    public Guid? CurrentPlayerId =>
        !RoundEnded && _currentIndex >= 0 ? _turnOrder[_currentIndex] : null;

    public IEnumerable<Guid> ActivePlayers => _turnOrder.Where(id => _lines[id].IsActive);

    /// <summary>Final per-player scores; valid once <see cref="RoundEnded"/> is true.</summary>
    public IReadOnlyDictionary<Guid, int> Scores =>
        _lines.ToDictionary(kv => kv.Key, kv => kv.Value.Score);

    // ---- Public turn API -------------------------------------------------

    /// <summary>Deals one card face-up to each player in turn order, resolving action cards as they appear.</summary>
    public IReadOnlyList<Flip7Event> DealInitial(TargetChooser? chooser = null)
    {
        if (_dealt) throw new InvalidOperationException("Initial deal already happened.");
        _dealt = true;

        var events = new List<Flip7Event>();
        var pick = chooser ?? DefaultChooser;

        foreach (var id in _turnOrder)
        {
            if (RoundEnded) break;
            var line = _lines[id];
            if (!line.IsActive) continue; // may have been frozen by an earlier deal
            ResolveDraw(line, pick, events, deferred: null);
            CheckRoundEnd(events);
        }

        if (!RoundEnded)
            _currentIndex = FirstActiveIndex();

        return events;
    }

    /// <summary>The current player takes one more card.</summary>
    public IReadOnlyList<Flip7Event> Hit(Guid playerId, TargetChooser? chooser = null)
    {
        RequireTurn(playerId);
        var events = new List<Flip7Event>();
        ResolveDraw(_lines[playerId], chooser ?? DefaultChooser, events, deferred: null);
        CheckRoundEnd(events);
        if (!RoundEnded) AdvanceTurn();
        return events;
    }

    /// <summary>The current player banks their points and exits the round.</summary>
    public IReadOnlyList<Flip7Event> Stay(Guid playerId)
    {
        RequireTurn(playerId);
        var line = _lines[playerId];
        if (!line.HasAnyCard)
            throw new InvalidOperationException("You must have at least one card to Stay.");

        line.Status = PlayerLineStatus.Stayed;
        var events = new List<Flip7Event>
        {
            new() { Type = Flip7EventType.Stayed, PlayerId = playerId },
        };
        CheckRoundEnd(events);
        if (!RoundEnded) AdvanceTurn();
        return events;
    }

    // ---- Resolution internals -------------------------------------------

    private void ResolveDraw(PlayerLine recipient, TargetChooser chooser, List<Flip7Event> events, List<ActionKind>? deferred)
    {
        if (!recipient.IsActive) return;

        var card = DrawCard();
        switch (card.Kind)
        {
            case CardKind.Number:
                ApplyNumber(recipient, card, events);
                break;

            case CardKind.Modifier:
                recipient.Modifiers.Add(card.Modifier!.Value);
                events.Add(new Flip7Event { Type = Flip7EventType.ModifierAdded, PlayerId = recipient.PlayerId, Card = card });
                break;

            case CardKind.Action:
                ResolveAction(recipient, card, chooser, events, deferred);
                break;
        }
    }

    private void ApplyNumber(PlayerLine recipient, Flip7Card card, List<Flip7Event> events)
    {
        int value = card.Number!.Value;

        if (recipient.HasNumber(value))
        {
            if (recipient.HasSecondChance)
            {
                recipient.HasSecondChance = false;
                _discard.Add(card); // duplicate discarded along with the Second Chance
                events.Add(new Flip7Event { Type = Flip7EventType.SecondChanceUsed, PlayerId = recipient.PlayerId, Card = card });
            }
            else
            {
                recipient.Status = PlayerLineStatus.Busted;
                _discard.Add(card);
                events.Add(new Flip7Event { Type = Flip7EventType.Busted, PlayerId = recipient.PlayerId, Card = card });
            }
            return;
        }

        recipient.Numbers.Add(value);
        events.Add(new Flip7Event { Type = Flip7EventType.NumberAdded, PlayerId = recipient.PlayerId, Card = card });

        if (recipient.Numbers.Count == Flip7Scoring.UniqueNumbersForFlip7)
        {
            recipient.AchievedFlip7 = true;
            events.Add(new Flip7Event { Type = Flip7EventType.Flip7Achieved, PlayerId = recipient.PlayerId });
        }
    }

    private void ResolveAction(PlayerLine drawer, Flip7Card card, TargetChooser chooser, List<Flip7Event> events, List<ActionKind>? deferred)
    {
        var action = card.Action!.Value;
        switch (action)
        {
            case ActionKind.Freeze:
                if (deferred != null) { deferred.Add(action); _discard.Add(card); return; }
                ResolveFreeze(drawer, chooser, events);
                _discard.Add(card);
                break;

            case ActionKind.FlipThree:
                if (deferred != null) { deferred.Add(action); _discard.Add(card); return; }
                ResolveFlipThree(drawer, chooser, events);
                _discard.Add(card);
                break;

            case ActionKind.SecondChance:
                // Kept in front of a player (tracked by the HasSecondChance flag)
                // or discarded inside the resolver — never auto-discarded here.
                ResolveSecondChance(drawer, card, chooser, events);
                break;
        }
    }

    private void ResolveFreeze(PlayerLine drawer, TargetChooser chooser, List<Flip7Event> events)
    {
        var candidates = ActiveCandidates();
        var targetId = ChooseTarget(ActionKind.Freeze, drawer.PlayerId, candidates, chooser);
        var target = _lines[targetId];
        target.Status = PlayerLineStatus.Frozen;
        events.Add(new Flip7Event
        {
            Type = Flip7EventType.Frozen,
            PlayerId = targetId,
            SourcePlayerId = drawer.PlayerId,
        });
    }

    private void ResolveFlipThree(PlayerLine drawer, TargetChooser chooser, List<Flip7Event> events)
    {
        var candidates = ActiveCandidates();
        var targetId = ChooseTarget(ActionKind.FlipThree, drawer.PlayerId, candidates, chooser);
        var target = _lines[targetId];

        events.Add(new Flip7Event
        {
            Type = Flip7EventType.FlipThreeStarted,
            PlayerId = targetId,
            SourcePlayerId = drawer.PlayerId,
        });

        var deferred = new List<ActionKind>();
        for (int i = 0; i < 3; i++)
        {
            if (!target.IsActive || target.AchievedFlip7) break;
            ResolveDraw(target, chooser, events, deferred);
        }

        // Nested Freeze / Flip Three resolve only if the player survived the sequence.
        if (target.IsActive && !target.AchievedFlip7)
        {
            foreach (var pending in deferred)
            {
                if (!target.IsActive || target.AchievedFlip7) break;
                if (pending == ActionKind.Freeze) ResolveFreeze(target, chooser, events);
                else ResolveFlipThree(target, chooser, events);
            }
        }
    }

    private void ResolveSecondChance(PlayerLine drawer, Flip7Card card, TargetChooser chooser, List<Flip7Event> events)
    {
        if (!drawer.HasSecondChance)
        {
            drawer.HasSecondChance = true;
            events.Add(new Flip7Event { Type = Flip7EventType.SecondChanceGained, PlayerId = drawer.PlayerId });
            return;
        }

        // Already holding one — pass to another active player who lacks one, else discard.
        var candidates = _turnOrder
            .Where(id => id != drawer.PlayerId && _lines[id].IsActive && !_lines[id].HasSecondChance)
            .ToList();

        if (candidates.Count == 0)
        {
            _discard.Add(card);
            events.Add(new Flip7Event { Type = Flip7EventType.SecondChanceDiscarded, PlayerId = drawer.PlayerId });
            return;
        }

        var targetId = ChooseTarget(ActionKind.SecondChance, drawer.PlayerId, candidates, chooser);
        _lines[targetId].HasSecondChance = true;
        events.Add(new Flip7Event
        {
            Type = Flip7EventType.SecondChancePassed,
            PlayerId = targetId,
            SourcePlayerId = drawer.PlayerId,
        });
    }

    // ---- Helpers ---------------------------------------------------------

    // Freeze / Flip Three may target any active player, including the drawer
    // (who is necessarily active at this point — so they are always a candidate).
    private List<Guid> ActiveCandidates() =>
        _turnOrder.Where(id => _lines[id].IsActive).ToList();

    private static Guid ChooseTarget(ActionKind action, Guid drawer, IReadOnlyList<Guid> candidates, TargetChooser chooser)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException($"No valid target for {action}.");
        if (candidates.Count == 1)
            return candidates[0]; // solo / last-player self-targeting falls out here

        var chosen = chooser(action, drawer, candidates);
        if (!candidates.Contains(chosen))
            throw new ArgumentException($"Chooser returned {chosen}, which is not an active candidate for {action}.");
        return chosen;
    }

    private static Guid DefaultChooser(ActionKind action, Guid drawer, IReadOnlyList<Guid> candidates) =>
        throw new InvalidOperationException(
            $"{action} drawn by {drawer} needs a target choice among {candidates.Count} candidates, but no TargetChooser was supplied.");

    private Flip7Card DrawCard()
    {
        if (_drawPile.Count == 0)
        {
            if (_discard.Count == 0)
                throw new InvalidOperationException("Deck exhausted: no cards left to draw.");
            ReshuffleDiscardIntoDraw();
        }

        var card = _drawPile[0];
        _drawPile.RemoveAt(0);
        return card;
    }

    private void ReshuffleDiscardIntoDraw()
    {
        _drawPile.AddRange(_discard);
        _discard.Clear();
        for (int i = _drawPile.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
        }
    }

    private void CheckRoundEnd(List<Flip7Event> events)
    {
        if (RoundEnded) return;

        if (_lines.Values.Any(l => l.AchievedFlip7))
        {
            EndRound(RoundEndReason.Flip7, events);
            return;
        }

        if (!_lines.Values.Any(l => l.IsActive))
            EndRound(RoundEndReason.AllInactive, events);
    }

    private void EndRound(RoundEndReason reason, List<Flip7Event> events)
    {
        RoundEnded = true;
        EndReason = reason;
        _currentIndex = -1;
        events.Add(new Flip7Event { Type = Flip7EventType.RoundEnded, Detail = reason.ToString() });
    }

    private void AdvanceTurn()
    {
        int n = _turnOrder.Count;
        for (int step = 1; step <= n; step++)
        {
            int idx = (_currentIndex + step) % n;
            if (_lines[_turnOrder[idx]].IsActive)
            {
                _currentIndex = idx;
                return;
            }
        }
        _currentIndex = -1;
    }

    private int FirstActiveIndex()
    {
        for (int i = 0; i < _turnOrder.Count; i++)
            if (_lines[_turnOrder[i]].IsActive)
                return i;
        return -1;
    }

    private void RequireTurn(Guid playerId)
    {
        if (RoundEnded)
            throw new InvalidOperationException("The round has ended.");
        if (CurrentPlayerId != playerId)
            throw new InvalidOperationException($"It is not {playerId}'s turn (current: {CurrentPlayerId}).");
    }
}
