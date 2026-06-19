using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Flip7Server.Data;
using Flip7Server.DTOs;
using Flip7Server.Game;
using Flip7Server.Models;
using Flip7Server.Services;

namespace Flip7Server.Tests.Services;

public class Flip7MultiplayerTests
{
    private static readonly Guid Human = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Flip7Card N(int v) => Flip7Card.OfNumber(v);

    private sealed class FixedDeckShuffler : IFlip7DeckShuffler
    {
        private readonly List<Flip7Card> _deck;
        public FixedDeckShuffler(params Flip7Card[] deck) => _deck = deck.ToList();
        public List<Flip7Card> CreateShuffledDeck() => _deck.ToList();
    }

    private sealed class StubAiClient : IFlip7AiClient
    {
        private readonly string _action;
        public StubAiClient(string action) => _action = action;
        public Task<string> DecideHitOrStayAsync(Flip7AiMoveRequest request, CancellationToken ct = default) =>
            Task.FromResult(_action);
    }

    private sealed class Harness
    {
        private readonly string _dbName = $"flip7-{Guid.NewGuid()}";
        private readonly Flip7Card[] _deck;
        private readonly string _aiAction;
        public Harness(string aiAction, params Flip7Card[] deck) { _aiAction = aiAction; _deck = deck; }
        public Flip7GameService Svc() =>
            new(new Flip7DbContext(new DbContextOptionsBuilder<Flip7DbContext>().UseInMemoryDatabase(_dbName).Options),
                new FixedDeckShuffler(_deck), new StubAiClient(_aiAction));
    }

    private static IReadOnlyList<Flip7AiSpec> OneAi(string style = "risky", string difficulty = "hard") =>
        new[] { new Flip7AiSpec { Style = style, Difficulty = difficulty } };

    [Fact]
    public async Task Create_vs_ai_deals_and_leaves_the_opening_ai_on_turn_for_the_hub_to_animate()
    {
        // Dealer seat 0 → turn order [AI, human]. The AI is dealt but does NOT act
        // at creation: opening turns are deferred so the hub can animate them.
        var h = new Harness("stay", N(4), N(5), N(6), N(7));
        var state = await h.Svc().CreateGameAsync(Flip7GameMode.VsAi, Human, "alice", OneAi(), null);

        state.Status.Should().Be("InProgress");
        state.RoundNumber.Should().Be(1);
        state.Players.Should().HaveCount(2);

        var ai = state.Players.Single(p => p.IsAi);
        ai.AiStyle.Should().Be("risky");
        ai.AiDifficulty.Should().Be("hard");
        ai.Numbers.Should().Equal(4);          // dealt
        ai.Status.Should().Be("Active");        // has NOT acted yet
        state.CurrentPlayerId.Should().Be(ai.Id); // AI is first; hub drives it

        // Driving the opening AI (what the hub does on connect) plays it out: the
        // stub "stay" banks and passes the turn to the human.
        Flip7GameStateDto? last = null;
        await h.Svc().DriveAiAsync(state.Id, s => { last = s; return Task.CompletedTask; });

        last.Should().NotBeNull();
        last!.Players.Single(p => p.IsAi).Status.Should().Be("Stayed");
        last.CurrentPlayerId.Should().Be(last.Players.Single(p => !p.IsAi).Id);
    }

    [Fact]
    public async Task Human_stay_ends_round_and_both_players_bank()
    {
        var h = new Harness("stay", N(4), N(5), N(6), N(7));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.VsAi, Human, "alice", OneAi(), null);
        await h.Svc().DriveAiAsync(created.Id, _ => Task.CompletedTask); // opening AI stays → human's turn

        var state = await h.Svc().StayAsync(created.Id, Human);

        state.RoundEnded.Should().BeTrue();
        state.Players.Single(p => p.IsAi).CumulativeScore.Should().Be(4);
        state.Players.Single(p => !p.IsAi).CumulativeScore.Should().Be(5);
        state.Status.Should().Be("InProgress"); // below 200 target
    }

    [Fact]
    public async Task Ai_chains_turns_to_flip7_once_the_human_is_inactive()
    {
        // One card per turn: turn order [AI, human]. Deal AI=1, human=2. The opening
        // AI drive hits to [1,3] then passes. Human stays; with only the AI active it
        // chains 4,5,6,7,8 → 7 unique → Flip 7.
        var h = new Harness("hit", N(1), N(2), N(3), N(4), N(5), N(6), N(7), N(8));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.VsAi, Human, "alice", OneAi(), null);
        created.Players.Single(p => p.IsAi).Numbers.Should().Equal(1); // dealt only, not acted

        Flip7GameStateDto? afterOpening = null;
        await h.Svc().DriveAiAsync(created.Id, s => { afterOpening = s; return Task.CompletedTask; });
        afterOpening!.Players.Single(p => p.IsAi).Numbers.Should().Equal(1, 3); // hit once, then passed

        var state = await h.Svc().StayAsync(created.Id, Human);

        state.RoundEnded.Should().BeTrue();
        state.RoundEndReason.Should().Be("Flip7");
        var ai = state.Players.Single(p => p.IsAi);
        ai.AchievedFlip7.Should().BeTrue();
        ai.CumulativeScore.Should().Be(1 + 3 + 4 + 5 + 6 + 7 + 8 + 15);
        state.Players.Single(p => !p.IsAi).CumulativeScore.Should().Be(2); // banked, not busted
    }

    [Fact]
    public async Task Ai_turns_stream_one_update_per_beat_when_onUpdate_is_supplied()
    {
        // Same setup as the Flip 7 chain: after the human stays, only the AI is
        // active and it hits 4,5,6,7,8 → Flip 7. With an onUpdate callback the
        // service should surface each beat (the human's stay, then every AI turn)
        // instead of one combined result.
        var h = new Harness("hit", N(1), N(2), N(3), N(4), N(5), N(6), N(7), N(8));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.VsAi, Human, "alice", OneAi(), null);
        await h.Svc().DriveAiAsync(created.Id, _ => Task.CompletedTask); // opening AI turn → human's turn

        var updates = new List<Flip7GameStateDto>();
        var final = await h.Svc().StayAsync(created.Id, Human, state =>
        {
            updates.Add(state);
            return Task.CompletedTask;
        });

        updates.Should().HaveCountGreaterThan(1);                 // streamed, not one shot
        updates[0].Players.Single(p => !p.IsAi).Status.Should().Be("Stayed"); // human's beat first
        updates.Should().OnlyContain(u => u.Events.Count > 0);    // every beat carries its events
        updates.Last().RoundEnded.Should().BeTrue();
        final.RoundEnded.Should().BeTrue();
        final.Players.Single(p => p.IsAi).AchievedFlip7.Should().BeTrue();
    }

    [Fact]
    public async Task Create_solo_via_create_game_is_rejected()
    {
        var h = new Harness("stay", N(3), N(4));
        var act = async () => await h.Svc().CreateGameAsync(Flip7GameMode.Solo, Human, "alice", OneAi(), null);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Vs_ai_requires_at_least_one_ai()
    {
        var h = new Harness("stay", N(3), N(4));
        var act = async () => await h.Svc().CreateGameAsync(Flip7GameMode.VsAi, Human, "alice", Array.Empty<Flip7AiSpec>(), null);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Online_game_waits_in_lobby_then_joins_and_starts()
    {
        var h = new Harness("stay", N(3), N(4), N(5), N(6), N(7), N(8));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.Online, Human, "alice", OneAi(), null);
        created.Status.Should().Be("Lobby");
        created.Players.Should().HaveCount(2); // creator + AI

        var joined = await h.Svc().JoinAsync(created.Id, Other, "bob");
        joined.Players.Should().HaveCount(3);
        joined.Status.Should().Be("Lobby");

        var started = await h.Svc().StartAsync(created.Id, Human);
        started.Status.Should().Be("InProgress");
        started.RoundNumber.Should().Be(1);
    }

    [Fact]
    public async Task Only_the_creator_can_start_an_online_game()
    {
        var h = new Harness("stay", N(3), N(4), N(5), N(6));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.Online, Human, "alice", OneAi(), null);

        var act = async () => await h.Svc().StartAsync(created.Id, Other);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Joining_a_non_online_game_is_rejected()
    {
        var h = new Harness("stay", N(3), N(4), N(5), N(6));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.VsAi, Human, "alice", OneAi(), null);

        var act = async () => await h.Svc().JoinAsync(created.Id, Other, "bob");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static Flip7Card Act(ActionKind a) => Flip7Card.OfAction(a);

    [Fact]
    public async Task Human_freeze_offers_both_active_players_and_freezes_the_chosen_opponent()
    {
        // Online, two humans, no AI. Dealer seat 0 → turn order [bob, alice].
        // Deal: bob=[4], alice=[5]. bob hits → 6; alice hits → Freeze (both active).
        var h = new Harness("stay", N(4), N(5), N(6), Act(ActionKind.Freeze));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.Online, Human, "alice", Array.Empty<Flip7AiSpec>(), null);
        await h.Svc().JoinAsync(created.Id, Other, "bob");
        var started = await h.Svc().StartAsync(created.Id, Human);

        var bob = started.Players.Single(p => p.UserId == Other);
        var alice = started.Players.Single(p => p.UserId == Human);
        started.CurrentPlayerId.Should().Be(bob.Id);  // bob acts first

        await h.Svc().HitAsync(created.Id, Other);     // bob → [4,6], turn → alice
        var pending = await h.Svc().HitAsync(created.Id, Human); // alice draws Freeze

        pending.PendingAction.Should().NotBeNull();
        pending.PendingAction!.Action.Should().Be("Freeze");
        pending.PendingAction.DrawerId.Should().Be(alice.Id);
        pending.PendingAction.CandidateIds.Should().BeEquivalentTo(new[] { alice.Id, bob.Id });

        var resolved = await h.Svc().ChooseTargetAsync(created.Id, Human, bob.Id);
        resolved.Players.Single(p => p.UserId == Other).Status.Should().Be("Frozen");
        resolved.Players.Single(p => p.UserId == Human).Status.Should().Be("Active");
    }

    [Fact]
    public async Task Only_the_drawer_can_choose_the_target()
    {
        var h = new Harness("stay", N(4), N(5), N(6), Act(ActionKind.Freeze));
        var created = await h.Svc().CreateGameAsync(Flip7GameMode.Online, Human, "alice", Array.Empty<Flip7AiSpec>(), null);
        await h.Svc().JoinAsync(created.Id, Other, "bob");
        await h.Svc().StartAsync(created.Id, Human);
        await h.Svc().HitAsync(created.Id, Other);
        var pending = await h.Svc().HitAsync(created.Id, Human); // alice drew the Freeze

        // bob is not the drawer; he cannot choose.
        var bob = pending.Players.Single(p => p.UserId == Other);
        var act = async () => await h.Svc().ChooseTargetAsync(created.Id, Other, bob.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class Flip7TargetSelectorTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static Flip7Card N(int v) => Flip7Card.OfNumber(v);

    [Fact]
    public void Flip_three_targets_the_opponent_with_the_most_numbers()
    {
        // Build a round where B has more numbers than C; drawer A flip-threes.
        var round = new Flip7Round(new[] { A, B, C },
            new[] { N(1), N(2), N(3), N(4), N(5) });
        round.DealInitial();      // A=[1], B=[2], C=[3]
        round.Hit(A);             // A=[1,4]; turn → B
        round.Hit(B);             // B=[2,5]; B now has 2 numbers, C has 1

        var target = Flip7TargetSelector.Choose(ActionKind.FlipThree, A, new[] { A, B, C }, round);
        target.Should().Be(B);
    }

    [Fact]
    public void Flip_three_targets_self_when_no_opponents_available()
    {
        var round = new Flip7Round(new[] { A }, new[] { N(1), N(2) });
        round.DealInitial();
        Flip7TargetSelector.Choose(ActionKind.FlipThree, A, new[] { A }, round).Should().Be(A);
    }

    [Fact]
    public void Freeze_targets_the_strongest_opponent()
    {
        var round = new Flip7Round(new[] { A, B, C },
            new[] { N(1), N(8), N(3), N(9) });
        round.DealInitial();      // A=[1], B=[8], C=[3]
        round.Hit(A);             // A=[1,9]; turn → B
        // B has [8] (1 number, score 8), C has [3] (1 number, score 3) → freeze B (higher score)
        var target = Flip7TargetSelector.Choose(ActionKind.Freeze, A, new[] { A, B, C }, round);
        target.Should().Be(B);
    }
}
