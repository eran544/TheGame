using FluentAssertions;
using Flip7Server.Game;

namespace Flip7Server.Tests.Game;

public class Flip7RoundTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static Flip7Card N(int v) => Flip7Card.OfNumber(v);
    private static Flip7Card Mod(ModifierKind m) => Flip7Card.OfModifier(m);
    private static Flip7Card Act(ActionKind a) => Flip7Card.OfAction(a);

    private static Flip7Round Solo(params Flip7Card[] deck) => new(new[] { A }, deck);
    private static Flip7Round Two(params Flip7Card[] deck) => new(new[] { A, B }, deck);

    // ---- Dealing & basic turn flow --------------------------------------

    [Fact]
    public void DealInitial_gives_each_player_one_card_in_turn_order()
    {
        var round = Two(N(3), N(4), N(9));
        round.DealInitial();

        round.Line(A).Numbers.Should().Equal(3);
        round.Line(B).Numbers.Should().Equal(4);
        round.CurrentPlayerId.Should().Be(A);
        round.DrawPileCount.Should().Be(1);
    }

    [Fact]
    public void DealInitial_cannot_run_twice()
    {
        var round = Solo(N(3), N(4));
        round.DealInitial();
        Action again = () => round.DealInitial();
        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BeginDeal_then_DealNext_hands_out_one_seat_per_call()
    {
        var round = Two(N(3), N(4), N(9));
        round.BeginDeal();
        round.Dealing.Should().BeTrue();
        round.CurrentPlayerId.Should().BeNull(); // no one is on turn mid-deal

        round.DealNext();                 // A gets 3
        round.Line(A).Numbers.Should().Equal(3);
        round.Line(B).Numbers.Should().BeEmpty();
        round.Dealing.Should().BeTrue();

        round.DealNext();                 // B gets 4 — last seat, deal finishes
        round.Line(B).Numbers.Should().Equal(4);
        round.Dealing.Should().BeFalse();
        round.CurrentPlayerId.Should().Be(A); // first active seat is on turn
    }

    [Fact]
    public void DealNext_resolves_a_dealt_action_within_its_one_beat()
    {
        // A's dealt card is a Flip Three (self-target) that draws 12, 12 → bust:
        // the reveal, the three draws, and the bust are all one animated beat.
        var round = Two(Act(ActionKind.FlipThree), N(12), N(12), N(5));
        TargetChooser self = (_, drawer, _) => drawer;
        round.BeginDeal();

        var beat = round.DealNext(self);  // A: Flip Three → 12, 12 (dup) → bust
        round.Line(A).Status.Should().Be(PlayerLineStatus.Busted);
        round.Line(A).BustedNumber.Should().Be(12);
        beat.Should().Contain(e => e.Type == Flip7EventType.FlipThreeStarted);
        beat.Should().Contain(e => e.Type == Flip7EventType.Busted && e.Card!.Number == 12);

        round.DealNext(self);             // B still gets its dealt card; deal finishes
        round.Line(B).Numbers.Should().Equal(5);
        round.Dealing.Should().BeFalse();
        round.CurrentPlayerId.Should().Be(B); // A busted out, B is first active
    }

    [Fact]
    public void Solo_hit_then_stay_banks_the_sum()
    {
        var round = Solo(N(3), N(5), N(8));
        round.DealInitial();           // A = [3]
        round.Hit(A);                  // A = [3, 5]
        round.Stay(A);

        round.RoundEnded.Should().BeTrue();
        round.EndReason.Should().Be(RoundEndReason.AllInactive);
        round.Line(A).Status.Should().Be(PlayerLineStatus.Stayed);
        round.Scores[A].Should().Be(8);
    }

    [Fact]
    public void Acting_out_of_turn_throws()
    {
        var round = Two(N(3), N(4));
        round.DealInitial();           // current = A
        Action wrong = () => round.Stay(B);
        wrong.Should().Throw<InvalidOperationException>();
    }

    // ---- Busting & Second Chance ----------------------------------------

    [Fact]
    public void Duplicate_number_busts_and_scores_zero()
    {
        var round = Solo(N(3), N(5), N(3));
        round.DealInitial();           // [3]
        round.Hit(A);                  // [3, 5]
        var events = round.Hit(A);     // draw 3 → duplicate → bust

        round.Line(A).Status.Should().Be(PlayerLineStatus.Busted);
        round.Scores[A].Should().Be(0);
        round.RoundEnded.Should().BeTrue();
        events.Should().Contain(e => e.Type == Flip7EventType.Busted);
    }

    [Fact]
    public void Second_chance_negates_a_bust_then_is_consumed()
    {
        var round = Solo(Act(ActionKind.SecondChance), N(3), N(3), N(5));
        round.DealInitial();           // draws Second Chance → kept
        round.Line(A).HasSecondChance.Should().BeTrue();

        round.Hit(A);                  // [3]
        var events = round.Hit(A);     // draw 3 again → negated by Second Chance

        round.Line(A).Status.Should().Be(PlayerLineStatus.Active);
        round.Line(A).HasSecondChance.Should().BeFalse();
        round.Line(A).Numbers.Should().Equal(3);
        events.Should().Contain(e => e.Type == Flip7EventType.SecondChanceUsed);

        round.Hit(A);                  // [3, 5]
        round.Stay(A);
        round.Scores[A].Should().Be(8);
    }

    [Fact]
    public void Held_second_chance_passes_to_another_active_player()
    {
        var round = Two(Act(ActionKind.SecondChance), N(4), Act(ActionKind.SecondChance));
        round.DealInitial();           // A gets SC (kept), B gets 4
        round.Line(A).HasSecondChance.Should().BeTrue();

        var events = round.Hit(A);     // A draws a 2nd SC → must pass to B (only eligible)
        round.Line(B).HasSecondChance.Should().BeTrue();
        round.Line(A).HasSecondChance.Should().BeTrue();
        events.Should().Contain(e => e.Type == Flip7EventType.SecondChancePassed && e.PlayerId == B);
    }

    [Fact]
    public void Held_second_chance_is_discarded_when_no_one_can_receive_it()
    {
        var round = Solo(Act(ActionKind.SecondChance), Act(ActionKind.SecondChance));
        round.DealInitial();           // A gets SC
        var events = round.Hit(A);     // draws a 2nd SC, alone → discarded

        round.Line(A).HasSecondChance.Should().BeTrue(); // still holds the first
        round.DiscardCount.Should().Be(1);
        events.Should().Contain(e => e.Type == Flip7EventType.SecondChanceDiscarded);
    }

    // ---- Flip 7 bonus ----------------------------------------------------

    [Fact]
    public void Seven_unique_numbers_triggers_flip7_bonus_and_ends_round()
    {
        var round = Solo(N(0), N(1), N(2), N(3), N(4), N(5), N(6));
        round.DealInitial();           // [0]
        for (int i = 0; i < 5; i++) round.Hit(A);   // up to [0..5]
        var events = round.Hit(A);     // 7th unique → Flip 7

        round.Line(A).AchievedFlip7.Should().BeTrue();
        round.RoundEnded.Should().BeTrue();
        round.EndReason.Should().Be(RoundEndReason.Flip7);
        round.Scores[A].Should().Be(0 + 1 + 2 + 3 + 4 + 5 + 6 + 15); // 36
        events.Should().Contain(e => e.Type == Flip7EventType.Flip7Achieved);
    }

    [Fact]
    public void Flip7_ends_round_for_everyone_and_others_bank_their_lines()
    {
        // Draw order: A=1, B=8 (deal); then A hits 2,3,4,5,6,7 with B staying between.
        var round = Two(N(1), N(8), N(2), N(3), N(4), N(5), N(6), N(7));
        round.DealInitial();           // A=[1], B=[8]

        round.Hit(A);                  // A=[1,2]; turn → B
        round.Stay(B);                 // B banks 8; turn → A (only active)
        round.Hit(A);                  // 3
        round.Hit(A);                  // 4
        round.Hit(A);                  // 5
        round.Hit(A);                  // 6
        round.Hit(A);                  // 7 → Flip 7

        round.RoundEnded.Should().BeTrue();
        round.EndReason.Should().Be(RoundEndReason.Flip7);
        round.Scores[A].Should().Be(28 + 15);
        round.Scores[B].Should().Be(8);   // banked via Stay, not busted
    }

    // ---- Modifiers -------------------------------------------------------

    [Fact]
    public void Modifiers_apply_with_times2_before_additive()
    {
        var round = Solo(N(3), Mod(ModifierKind.Times2), Mod(ModifierKind.Plus2), N(5));
        round.DealInitial();           // [3]
        round.Hit(A);                  // x2
        round.Hit(A);                  // +2
        round.Hit(A);                  // [3,5]
        round.Stay(A);

        round.Scores[A].Should().Be((3 + 5) * 2 + 2); // 18
    }

    // ---- Freeze ----------------------------------------------------------

    [Fact]
    public void Freeze_with_only_self_active_banks_and_exits()
    {
        var round = Solo(N(5), Act(ActionKind.Freeze));
        round.DealInitial();           // [5]
        var events = round.Hit(A);     // Freeze self → banked

        round.Line(A).Status.Should().Be(PlayerLineStatus.Frozen);
        round.RoundEnded.Should().BeTrue();
        round.Scores[A].Should().Be(5);
        events.Should().Contain(e => e.Type == Flip7EventType.Frozen && e.PlayerId == A);
    }

    [Fact]
    public void Freeze_targets_the_chosen_active_player()
    {
        var round = Two(N(3), N(4), Act(ActionKind.Freeze));
        round.DealInitial();           // A=[3], B=[4]
        TargetChooser pickB = (_, _, _) => B;

        round.Hit(A, pickB);           // A draws Freeze, targets B → B banks 4

        round.Line(B).Status.Should().Be(PlayerLineStatus.Frozen);
        round.Line(A).Status.Should().Be(PlayerLineStatus.Active);
        round.Scores[B].Should().Be(4);
        round.CurrentPlayerId.Should().Be(A); // B inactive, turn returns to A
    }

    // ---- Flip Three ------------------------------------------------------

    [Fact]
    public void Flip_three_draws_three_cards_for_the_target()
    {
        var round = Solo(N(2), Act(ActionKind.FlipThree), N(4), N(5), N(6));
        round.DealInitial();           // [2]
        round.Hit(A);                  // Flip Three (self) → draws 4,5,6
        round.Line(A).Numbers.Should().Equal(2, 4, 5, 6);
        round.Line(A).Status.Should().Be(PlayerLineStatus.Active);

        round.Stay(A);
        round.Scores[A].Should().Be(2 + 4 + 5 + 6); // 17
    }

    [Fact]
    public void Flip_three_stops_early_on_bust_leaving_remaining_cards()
    {
        var round = Solo(N(2), Act(ActionKind.FlipThree), N(2), N(9), N(9));
        round.DealInitial();           // [2]
        round.Hit(A);                  // Flip Three → draws 2 (dup) → bust, stops early

        round.Line(A).Status.Should().Be(PlayerLineStatus.Busted);
        round.Scores[A].Should().Be(0);
        round.DrawPileCount.Should().Be(2); // the two 9s were never drawn
    }

    [Fact]
    public void Flip_three_defers_a_nested_freeze_until_after_the_three_draws()
    {
        var round = Solo(N(2), Act(ActionKind.FlipThree), Act(ActionKind.Freeze), N(4), N(5));
        round.DealInitial();           // [2]
        round.Hit(A);                  // Flip Three → Freeze (deferred), 4, 5; then Freeze resolves

        round.Line(A).Numbers.Should().Equal(2, 4, 5);
        round.Line(A).Status.Should().Be(PlayerLineStatus.Frozen);
        round.RoundEnded.Should().BeTrue();
        round.Scores[A].Should().Be(11);
    }

    // ---- Round end & reshuffle ------------------------------------------

    [Fact]
    public void Round_ends_when_all_players_go_inactive()
    {
        var round = Two(N(3), N(4));
        round.DealInitial();
        round.Stay(A);
        round.RoundEnded.Should().BeFalse();
        round.Stay(B);

        round.RoundEnded.Should().BeTrue();
        round.EndReason.Should().Be(RoundEndReason.AllInactive);
        round.Scores[A].Should().Be(3);
        round.Scores[B].Should().Be(4);
    }

    [Fact]
    public void Draw_pile_reshuffles_from_discard_when_emptied_mid_round()
    {
        // A keeps a Second Chance, then repeatedly discards extra Second Chances,
        // emptying the draw pile and forcing a reshuffle of the discard.
        var round = Solo(N(5), Act(ActionKind.SecondChance), Act(ActionKind.SecondChance));
        round.DealInitial();           // [5]
        round.Hit(A);                  // SC kept
        round.Hit(A);                  // SC discarded → draw pile now empty, discard = 1

        round.DrawPileCount.Should().Be(0);
        round.DiscardCount.Should().Be(1);

        Action drawAfterEmpty = () => round.Hit(A); // must reshuffle discard, not throw
        drawAfterEmpty.Should().NotThrow();
        round.Line(A).IsActive.Should().BeTrue();
    }

    // ---- Interactive target choice (human drawers) ----------------------

    // A chooser that always defers to an interactive choice (as a human does).
    private static readonly TargetChooser Interactive = (_, _, _) => null;

    [Fact]
    public void Busted_event_carries_the_duplicate_card()
    {
        var round = Solo(N(3), N(5), N(3));
        round.DealInitial();
        round.Hit(A);
        var events = round.Hit(A);

        var bust = events.Single(e => e.Type == Flip7EventType.Busted);
        bust.Card!.Number.Should().Be(3);
    }

    [Fact]
    public void Second_chance_used_event_carries_the_duplicate_card()
    {
        var round = Solo(Act(ActionKind.SecondChance), N(4), N(4));
        round.DealInitial();          // Second Chance kept
        round.Hit(A);                 // [4]
        var events = round.Hit(A);    // duplicate 4 negated

        var used = events.Single(e => e.Type == Flip7EventType.SecondChanceUsed);
        used.Card!.Number.Should().Be(4);
    }

    [Fact]
    public void Action_card_reveal_emits_action_drawn_event()
    {
        var round = Solo(N(5), Act(ActionKind.Freeze));
        round.DealInitial();
        var events = round.Hit(A, Interactive);

        events.Should().Contain(e =>
            e.Type == Flip7EventType.ActionDrawn && e.Card!.Action == ActionKind.Freeze);
    }

    [Fact]
    public void Human_freeze_suspends_for_a_target_even_when_only_self_is_eligible()
    {
        var round = Solo(N(5), Act(ActionKind.Freeze));
        round.DealInitial();          // [5]
        round.Hit(A, Interactive);    // draws Freeze → suspends

        round.PendingAction.Should().NotBeNull();
        round.PendingAction!.Action.Should().Be(ActionKind.Freeze);
        round.PendingAction.DrawerId.Should().Be(A);
        round.PendingAction.Candidates.Should().Equal(A); // only self
        round.Line(A).Status.Should().Be(PlayerLineStatus.Active); // not yet resolved

        round.ResolveTarget(A, A);
        round.Line(A).Status.Should().Be(PlayerLineStatus.Frozen);
        round.RoundEnded.Should().BeTrue();
        round.Scores[A].Should().Be(5);
    }

    [Fact]
    public void Human_freeze_can_target_a_chosen_opponent()
    {
        var round = Two(N(3), N(4), Act(ActionKind.Freeze));
        round.DealInitial();          // A=[3], B=[4]
        round.Hit(A, Interactive);    // A draws Freeze → suspends with both candidates

        round.PendingAction!.Candidates.Should().BeEquivalentTo(new[] { A, B });

        round.ResolveTarget(A, B, Interactive);
        round.Line(B).Status.Should().Be(PlayerLineStatus.Frozen);
        round.Line(A).Status.Should().Be(PlayerLineStatus.Active);
        round.CurrentPlayerId.Should().Be(A); // B inactive, turn returns to A
    }

    [Fact]
    public void Cannot_hit_while_a_target_choice_is_pending()
    {
        var round = Solo(N(5), Act(ActionKind.Freeze), N(6));
        round.DealInitial();
        round.Hit(A, Interactive);    // suspends

        Action hitWhilePending = () => round.Hit(A, Interactive);
        hitWhilePending.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_target_rejects_a_non_candidate()
    {
        var round = Two(N(3), N(4), Act(ActionKind.Freeze));
        round.DealInitial();
        round.Hit(A, Interactive);

        Action wrong = () => round.ResolveTarget(A, Guid.NewGuid(), Interactive);
        wrong.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Human_flip_three_suspends_then_draws_three_for_the_target()
    {
        var round = Solo(N(2), Act(ActionKind.FlipThree), N(4), N(5), N(6));
        round.DealInitial();          // [2]
        round.Hit(A, Interactive);    // draws Flip Three → suspends
        round.PendingAction!.Action.Should().Be(ActionKind.FlipThree);

        round.ResolveTarget(A, A);    // self → draws 4,5,6
        round.Line(A).Numbers.Should().Equal(2, 4, 5, 6);
        round.PendingAction.Should().BeNull();
        round.CurrentPlayerId.Should().Be(A);
    }

    [Fact]
    public void Pending_action_survives_a_snapshot_round_trip()
    {
        var round = Two(N(3), N(4), Act(ActionKind.Freeze));
        round.DealInitial();
        round.Hit(A, Interactive);    // suspended on Freeze with [A, B]

        var restored = Flip7Round.Restore(round.Capture());
        restored.PendingAction.Should().NotBeNull();
        restored.PendingAction!.Action.Should().Be(ActionKind.Freeze);
        restored.PendingAction.Candidates.Should().BeEquivalentTo(new[] { A, B });

        restored.ResolveTarget(A, B, Interactive);
        restored.Line(B).Status.Should().Be(PlayerLineStatus.Frozen);
    }
}
