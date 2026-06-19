using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Flip7Server.Data;
using Flip7Server.Game;
using Flip7Server.Services;

namespace Flip7Server.Tests.Services;

public class Flip7GameServiceTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Returns a caller-controlled deck for every round, so tests are deterministic.</summary>
    private sealed class FixedDeckShuffler : IFlip7DeckShuffler
    {
        private readonly List<Flip7Card> _deck;
        public FixedDeckShuffler(params Flip7Card[] deck) => _deck = deck.ToList();
        public List<Flip7Card> CreateShuffledDeck() => _deck.ToList();
    }

    private static Flip7Card N(int v) => Flip7Card.OfNumber(v);

    /// <summary>
    /// Mirrors production scoping: every service call gets a fresh DbContext over
    /// a shared in-memory database, with a deterministic deck.
    /// </summary>
    private sealed class Harness
    {
        private readonly string _dbName = $"flip7-{Guid.NewGuid()}";
        private readonly Flip7Card[] _deck;

        public Harness(params Flip7Card[] deck) =>
            _deck = deck.Length > 0 ? deck : new[] { N(3), N(5), N(8), N(9), N(10) };

        public Flip7GameService Svc() =>
            new(new Flip7DbContext(new DbContextOptionsBuilder<Flip7DbContext>()
                    .UseInMemoryDatabase(_dbName).Options),
                new FixedDeckShuffler(_deck),
                new StubAiClient("stay"));
    }

    /// <summary>Returns a fixed AI decision; solo tests never invoke it (no AI players).</summary>
    private sealed class StubAiClient : IFlip7AiClient
    {
        private readonly string _action;
        public StubAiClient(string action) => _action = action;
        public Task<string> DecideHitOrStayAsync(Flip7AiMoveRequest request, CancellationToken ct = default) =>
            Task.FromResult(_action);
    }

    [Fact]
    public async Task Create_solo_starts_round_one_with_one_card_dealt()
    {
        var h = new Harness(N(3), N(5), N(8));

        var state = await h.Svc().CreateSoloAsync(User, "alice", null);

        state.Mode.Should().Be("Solo");
        state.Status.Should().Be("InProgress");
        state.TargetScore.Should().Be(200);
        state.RoundNumber.Should().Be(1);
        state.Players.Should().HaveCount(1);
        state.Players[0].Username.Should().Be("alice");
        state.Players[0].Numbers.Should().Equal(3);     // dealt one card
        state.CurrentPlayerId.Should().Be(state.Players[0].Id);
    }

    [Fact]
    public async Task Hit_then_stay_banks_the_sum_and_ends_the_round()
    {
        var h = new Harness(N(3), N(5), N(8));

        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);             // [3,5]
        var state = await h.Svc().StayAsync(created.Id, User);

        state.RoundEnded.Should().BeTrue();
        state.Players[0].Status.Should().Be("Stayed");
        state.Players[0].CumulativeScore.Should().Be(8);
        state.Status.Should().Be("InProgress"); // below target → awaits next round
    }

    [Fact]
    public async Task Duplicate_card_busts_for_zero()
    {
        var h = new Harness(N(3), N(5), N(3));

        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);             // [3,5]
        var state = await h.Svc().HitAsync(created.Id, User); // 3 dup → bust

        state.RoundEnded.Should().BeTrue();
        state.Players[0].Status.Should().Be("Busted");
        state.Players[0].CumulativeScore.Should().Be(0);
    }

    [Fact]
    public async Task Next_round_deals_a_fresh_round_after_the_previous_ended()
    {
        var h = new Harness(N(4), N(5), N(6));

        var created = await h.Svc().CreateSoloAsync(User, "alice", null); // [4]
        await h.Svc().StayAsync(created.Id, User);                        // bank 4, round ends
        var state = await h.Svc().NextRoundAsync(created.Id, User);

        state.RoundNumber.Should().Be(2);
        state.RoundEnded.Should().BeFalse();
        state.Players[0].Numbers.Should().Equal(4);                   // fresh deal
        state.Players[0].CumulativeScore.Should().Be(4);             // carried over
    }

    [Fact]
    public async Task Next_round_is_rejected_while_a_round_is_in_progress()
    {
        var h = new Harness(N(4), N(5), N(6));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);

        var act = async () => await h.Svc().NextRoundAsync(created.Id, User);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Game_completes_and_sets_winner_when_target_reached()
    {
        var h = new Harness(N(3), N(9));                      // stay banks 3 each round

        var created = await h.Svc().CreateSoloAsync(User, "alice", 5); // small target
        await h.Svc().StayAsync(created.Id, User);           // cumulative 3
        await h.Svc().NextRoundAsync(created.Id, User);
        var state = await h.Svc().StayAsync(created.Id, User); // cumulative 6 ≥ 5

        state.Status.Should().Be("Completed");
        state.WinnerId.Should().Be(state.Players[0].Id);
        state.Players[0].CumulativeScore.Should().Be(6);
    }

    [Fact]
    public async Task State_persists_and_is_retrievable()
    {
        var h = new Harness(N(7), N(2), N(9));

        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);             // [7,2]

        var fetched = await h.Svc().GetStateAsync(created.Id, User);
        fetched.Should().NotBeNull();
        fetched!.Players[0].Numbers.Should().Equal(7, 2);
        fetched.RoundNumber.Should().Be(1);
    }

    [Fact]
    public async Task A_non_participant_cannot_read_or_act()
    {
        var h = new Harness(N(3), N(5), N(8));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        var stranger = Guid.NewGuid();

        var read = async () => await h.Svc().GetStateAsync(created.Id, stranger);
        var act = async () => await h.Svc().HitAsync(created.Id, stranger);
        await read.Should().ThrowAsync<UnauthorizedAccessException>();
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Acting_on_a_missing_game_throws_key_not_found()
    {
        var h = new Harness();
        var act = async () => await h.Svc().HitAsync(Guid.NewGuid(), User);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static Flip7Card Act(ActionKind a) => Flip7Card.OfAction(a);

    [Fact]
    public async Task Bust_event_reports_the_duplicate_card()
    {
        var h = new Harness(N(3), N(5), N(3));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);              // [3,5]
        var state = await h.Svc().HitAsync(created.Id, User);  // 3 dup → bust

        state.Events.Should().Contain(e => e.Type == "Busted" && e.Card == "3");
    }

    [Fact]
    public async Task Busted_line_remembers_the_duplicate_number_on_the_player_state()
    {
        var h = new Harness(N(3), N(5), N(3));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);              // [3,5]
        var state = await h.Svc().HitAsync(created.Id, User);  // 3 dup → bust

        state.Players[0].Status.Should().Be("Busted");
        state.Players[0].BustedNumber.Should().Be(3);

        // Survives a reload (it is part of the persisted round snapshot).
        var fetched = await h.Svc().GetStateAsync(created.Id, User);
        fetched!.Players[0].BustedNumber.Should().Be(3);
    }

    [Fact]
    public async Task Solo_freeze_suspends_for_a_self_target_choice()
    {
        var h = new Harness(N(5), Act(ActionKind.Freeze));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null); // [5]

        var state = await h.Svc().HitAsync(created.Id, User);  // draws Freeze

        state.PendingAction.Should().NotBeNull();
        state.PendingAction!.Action.Should().Be("Freeze");
        state.PendingAction.DrawerId.Should().Be(state.Players[0].Id);
        state.PendingAction.CandidateIds.Should().Equal(state.Players[0].Id); // only self
        state.Events.Should().Contain(e => e.Type == "ActionDrawn" && e.Card == "Freeze");
        state.Players[0].Status.Should().Be("Active");         // not yet resolved
    }

    [Fact]
    public async Task Choosing_the_self_target_resolves_the_freeze()
    {
        var h = new Harness(N(5), Act(ActionKind.Freeze));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        var pending = await h.Svc().HitAsync(created.Id, User);

        var state = await h.Svc().ChooseTargetAsync(created.Id, User, pending.Players[0].Id);

        state.PendingAction.Should().BeNull();
        state.Players[0].Status.Should().Be("Frozen");
        state.RoundEnded.Should().BeTrue();
        state.Players[0].CumulativeScore.Should().Be(5);       // banked on freeze
        state.Events.Should().Contain(e => e.Type == "Frozen");
    }

    [Fact]
    public async Task Hitting_while_a_target_choice_is_pending_is_rejected()
    {
        var h = new Harness(N(5), Act(ActionKind.Freeze), N(6));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);              // suspends on Freeze

        var act = async () => await h.Svc().HitAsync(created.Id, User);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Pending_freeze_survives_persistence()
    {
        var h = new Harness(N(5), Act(ActionKind.Freeze));
        var created = await h.Svc().CreateSoloAsync(User, "alice", null);
        await h.Svc().HitAsync(created.Id, User);

        // Fresh service instance / DbContext: state must rehydrate the pending action.
        var fetched = await h.Svc().GetStateAsync(created.Id, User);
        fetched!.PendingAction.Should().NotBeNull();
        fetched.PendingAction!.Action.Should().Be("Freeze");
    }
}
