namespace Flip7Server.Game;

/// <summary>Why a round ended.</summary>
public enum RoundEndReason
{
    None,
    AllInactive,
    Flip7,
}

/// <summary>
/// Chooses the target of a drawn Freeze / Flip Three (and of a passed Second
/// Chance when more than one player can receive it). Returning a player id
/// resolves immediately; returning null suspends the round in a
/// <see cref="Flip7Round.PendingAction"/> state until
/// <see cref="Flip7Round.ResolveTarget"/> supplies the choice — that is how
/// human players get an interactive picker even when the only legal target is
/// themselves.
/// </summary>
public delegate Guid? TargetChooser(ActionKind action, Guid drawingPlayer, IReadOnlyList<Guid> candidates);

/// <summary>An action card waiting for its target to be chosen.</summary>
public sealed record PendingActionInfo(ActionKind Action, Guid DrawerId, IReadOnlyList<Guid> Candidates);

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

    // Action cards drawn but not yet resolved (head = next to resolve). The
    // head becomes PendingAction while a target choice is outstanding.
    private readonly List<(ActionKind Action, Guid DrawerId)> _actionQueue = new();

    // Seat index the initial deal is paused at (-1 = deal not in progress).
    private int _dealCursor = -1;

    // A Hit that paused on a target choice still owes its turn advancement.
    private bool _advanceTurnAfterResolve;

    /// <summary>The action card whose target must be chosen before play continues.</summary>
    public PendingActionInfo? PendingAction { get; private set; }

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
                BustedNumber = l.BustedNumber,
            };
        }).ToList(),
        DrawPile = _drawPile.ToList(),
        Discard = _discard.ToList(),
        CurrentIndex = _currentIndex,
        Dealt = _dealt,
        RoundEnded = RoundEnded,
        EndReason = EndReason,
        ActionQueue = _actionQueue
            .Select(a => new PendingActionSnapshot { Action = a.Action, DrawerId = a.DrawerId })
            .ToList(),
        DealCursor = _dealCursor,
        AdvanceTurnAfterResolve = _advanceTurnAfterResolve,
        PendingCandidates = PendingAction?.Candidates.ToList(),
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
            line.BustedNumber = ls.BustedNumber;
        }
        round._discard.AddRange(s.Discard);
        round._currentIndex = s.CurrentIndex;
        round._dealt = s.Dealt;
        round.RoundEnded = s.RoundEnded;
        round.EndReason = s.EndReason;
        round._actionQueue.AddRange(s.ActionQueue.Select(a => (a.Action, a.DrawerId)));
        round._dealCursor = s.DealCursor;
        round._advanceTurnAfterResolve = s.AdvanceTurnAfterResolve;
        if (s.PendingCandidates is { Count: > 0 } && round._actionQueue.Count > 0)
        {
            var head = round._actionQueue[0];
            round.PendingAction = new PendingActionInfo(head.Action, head.DrawerId, s.PendingCandidates);
        }
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

    /// <summary>
    /// Deals one card face-up to each player in turn order, resolving action
    /// cards as they appear. May suspend on <see cref="PendingAction"/>; the
    /// deal resumes automatically after <see cref="ResolveTarget"/>.
    /// </summary>
    public IReadOnlyList<Flip7Event> DealInitial(TargetChooser? chooser = null)
    {
        if (_dealt) throw new InvalidOperationException("Initial deal already happened.");
        _dealt = true;
        _dealCursor = 0;

        var events = new List<Flip7Event>();
        ContinueDeal(chooser ?? DefaultChooser, events);
        return events;
    }

    /// <summary>The current player takes one more card.</summary>
    public IReadOnlyList<Flip7Event> Hit(Guid playerId, TargetChooser? chooser = null)
    {
        RequireTurn(playerId);
        var pick = chooser ?? DefaultChooser;
        var events = new List<Flip7Event>();
        ResolveDraw(_lines[playerId], events, deferred: null);
        PumpActions(pick, events);
        CheckRoundEnd(events);

        if (PendingAction is not null)
        {
            _advanceTurnAfterResolve = true; // owed once the target is chosen
            return events;
        }

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

    /// <summary>
    /// Supplies the target for the suspended <see cref="PendingAction"/> and
    /// resumes whatever the pause interrupted (the initial deal, the queue of
    /// further drawn actions, or the turn rotation).
    /// </summary>
    public IReadOnlyList<Flip7Event> ResolveTarget(Guid playerId, Guid targetId, TargetChooser? chooser = null)
    {
        if (PendingAction is null)
            throw new InvalidOperationException("No action card is awaiting a target.");
        if (PendingAction.DrawerId != playerId)
            throw new InvalidOperationException("Only the player who drew the action card chooses its target.");
        if (!PendingAction.Candidates.Contains(targetId))
            throw new InvalidOperationException("That player cannot be targeted.");

        var pick = chooser ?? DefaultChooser;
        var events = new List<Flip7Event>();

        var pending = PendingAction;
        PendingAction = null;
        _actionQueue.RemoveAt(0); // the pending action is always the queue head

        ApplyAction(pending.Action, pending.DrawerId, targetId, events);
        CheckRoundEnd(events);
        PumpActions(pick, events);

        if (PendingAction is null && !RoundEnded)
        {
            if (_dealCursor >= 0)
            {
                ContinueDeal(pick, events);
            }
            else if (_advanceTurnAfterResolve)
            {
                _advanceTurnAfterResolve = false;
                AdvanceTurn();
            }
        }

        return events;
    }

    // ---- Resolution internals -------------------------------------------

    /// <summary>Deals to the seats from the paused cursor onward; suspends on a target choice.</summary>
    private void ContinueDeal(TargetChooser chooser, List<Flip7Event> events)
    {
        while (_dealCursor >= 0 && _dealCursor < _turnOrder.Count && !RoundEnded)
        {
            var line = _lines[_turnOrder[_dealCursor]];
            _dealCursor++;
            if (!line.IsActive) continue; // may have been frozen by an earlier deal

            ResolveDraw(line, events, deferred: null);
            PumpActions(chooser, events);
            CheckRoundEnd(events);
            if (PendingAction is not null) return; // resume from the cursor later
        }

        _dealCursor = -1;
        if (!RoundEnded)
            _currentIndex = FirstActiveIndex();
    }

    private void ResolveDraw(PlayerLine recipient, List<Flip7Event> events, List<ActionKind>? deferred)
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
                ResolveAction(recipient, card, events, deferred);
                break;
        }
    }

    /// <summary>
    /// Resolves queued Freeze / Flip Three cards in order, consulting the
    /// chooser for each target. A null choice suspends on the queue head as
    /// <see cref="PendingAction"/>.
    /// </summary>
    private void PumpActions(TargetChooser chooser, List<Flip7Event> events)
    {
        while (PendingAction is null && _actionQueue.Count > 0)
        {
            if (RoundEnded)
            {
                _actionQueue.Clear();
                return;
            }

            var (action, drawerId) = _actionQueue[0];
            var candidates = ActiveCandidates();
            if (candidates.Count == 0)
            {
                _actionQueue.RemoveAt(0); // fizzles — nobody left to target
                continue;
            }

            var choice = chooser(action, drawerId, candidates);
            if (choice is null)
            {
                PendingAction = new PendingActionInfo(action, drawerId, candidates);
                return;
            }

            if (!candidates.Contains(choice.Value))
                throw new ArgumentException($"Chooser returned {choice}, which is not an active candidate for {action}.");

            _actionQueue.RemoveAt(0);
            ApplyAction(action, drawerId, choice.Value, events);
            CheckRoundEnd(events);
        }
    }

    private void ApplyAction(ActionKind action, Guid drawerId, Guid targetId, List<Flip7Event> events)
    {
        if (action == ActionKind.Freeze)
        {
            var target = _lines[targetId];
            if (!target.IsActive) return; // target left the round while queued
            target.Status = PlayerLineStatus.Frozen;
            events.Add(new Flip7Event
            {
                Type = Flip7EventType.Frozen,
                PlayerId = targetId,
                SourcePlayerId = drawerId,
            });
            return;
        }

        // Flip Three
        var line = _lines[targetId];
        if (!line.IsActive) return;

        events.Add(new Flip7Event
        {
            Type = Flip7EventType.FlipThreeStarted,
            PlayerId = targetId,
            SourcePlayerId = drawerId,
        });

        var deferred = new List<ActionKind>();
        for (int i = 0; i < 3; i++)
        {
            if (!line.IsActive || line.AchievedFlip7) break;
            ResolveDraw(line, events, deferred);
        }

        // Nested Freeze / Flip Three resolve after the three draws, and only if
        // the player survived the sequence. They go to the queue front so they
        // resolve (with their own target choice) before older pending actions.
        if (line.IsActive && !line.AchievedFlip7)
        {
            for (int i = deferred.Count - 1; i >= 0; i--)
                _actionQueue.Insert(0, (deferred[i], targetId));
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
                recipient.BustedNumber = value; // remembered so the UI can show the duplicate
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

    private void ResolveAction(PlayerLine drawer, Flip7Card card, List<Flip7Event> events, List<ActionKind>? deferred)
    {
        var action = card.Action!.Value;

        if (action == ActionKind.SecondChance)
        {
            // Kept in front of a player (tracked by the HasSecondChance flag)
            // or discarded inside the resolver — resolves inline, never queues.
            ResolveSecondChance(drawer, card, events);
            return;
        }

        // Freeze / Flip Three: reveal the card, then queue it for resolution
        // (the target choice happens in PumpActions). During a Flip Three the
        // rulebook defers these until after the three draws.
        _discard.Add(card);
        events.Add(new Flip7Event { Type = Flip7EventType.ActionDrawn, PlayerId = drawer.PlayerId, Card = card });

        if (deferred != null)
            deferred.Add(action);
        else
            _actionQueue.Add((action, drawer.PlayerId));
    }

    private void ResolveSecondChance(PlayerLine drawer, Flip7Card card, List<Flip7Event> events)
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

        // Receiving player is picked automatically (lowest-id active without one);
        // this is the one action that never pauses for a choice.
        var targetId = Flip7TargetSelector.Choose(ActionKind.SecondChance, drawer.PlayerId, candidates, this);
        _lines[targetId].HasSecondChance = true;
        events.Add(new Flip7Event
        {
            Type = Flip7EventType.SecondChancePassed,
            PlayerId = targetId,
            SourcePlayerId = drawer.PlayerId,
        });
    }

    // ---- Helpers ---------------------------------------------------------

    // Freeze / Flip Three may target any active player, including the drawer.
    private List<Guid> ActiveCandidates() =>
        _turnOrder.Where(id => _lines[id].IsActive).ToList();

    // Used when no chooser is supplied (tests / solo flows): self-target when
    // the choice is forced, otherwise demand an explicit chooser.
    private static Guid? DefaultChooser(ActionKind action, Guid drawer, IReadOnlyList<Guid> candidates) =>
        candidates.Count == 1
            ? candidates[0]
            : throw new InvalidOperationException(
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
        _dealCursor = -1;
        _advanceTurnAfterResolve = false;
        _actionQueue.Clear();
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
        if (PendingAction is not null)
            throw new InvalidOperationException("An action card is awaiting a target choice.");
        if (CurrentPlayerId != playerId)
            throw new InvalidOperationException($"It is not {playerId}'s turn (current: {CurrentPlayerId}).");
    }
}
