using System.Text.Json;
using FluentAssertions;
using Flip7Server.Game;

namespace Flip7Server.Tests.Game;

public class Flip7SnapshotTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static Flip7Card N(int v) => Flip7Card.OfNumber(v);

    [Fact]
    public void Capture_then_restore_preserves_round_state_and_continues_play()
    {
        var deck = new[] { N(3), N(4), N(5), N(6), N(7) };
        var round = new Flip7Round(new[] { A, B }, deck);
        round.DealInitial();           // A=[3], B=[4], current=A
        round.Hit(A);                  // A=[3,5], current=B

        var snapshot = round.Capture();
        var restored = Flip7Round.Restore(snapshot);

        restored.CurrentPlayerId.Should().Be(B);
        restored.Line(A).Numbers.Should().Equal(3, 5);
        restored.Line(B).Numbers.Should().Equal(4);
        restored.DrawPileCount.Should().Be(round.DrawPileCount);

        // The restored round is a working state machine.
        restored.Hit(B);              // B=[4,6]
        restored.Line(B).Numbers.Should().Equal(4, 6);
    }

    [Fact]
    public void Snapshot_survives_a_json_round_trip()
    {
        var round = new Flip7Round(new[] { A }, new[] { N(2), N(8), N(9) });
        round.DealInitial();
        round.Hit(A);                 // A=[2,8]

        var json = JsonSerializer.Serialize(round.Capture());
        var snapshot = JsonSerializer.Deserialize<Flip7RoundSnapshot>(json)!;
        var restored = Flip7Round.Restore(snapshot);

        restored.Line(A).Numbers.Should().Equal(2, 8);
        restored.CurrentPlayerId.Should().Be(A);
        restored.Hit(A);              // draws 9 → A=[2,8,9]
        restored.Line(A).Numbers.Should().Equal(2, 8, 9);
    }

    [Fact]
    public void Snapshot_preserves_modifiers_second_chance_and_status()
    {
        var round = new Flip7Round(new[] { A },
            new[] { Flip7Card.OfModifier(ModifierKind.Times2), Flip7Card.OfAction(ActionKind.SecondChance), N(5) });
        round.DealInitial();          // x2 modifier
        round.Hit(A);                 // Second Chance kept

        var snapshot = JsonSerializer.Deserialize<Flip7RoundSnapshot>(
            JsonSerializer.Serialize(round.Capture()))!;
        var restored = Flip7Round.Restore(snapshot);

        restored.Line(A).Modifiers.Should().Equal(ModifierKind.Times2);
        restored.Line(A).HasSecondChance.Should().BeTrue();
        restored.Line(A).Status.Should().Be(PlayerLineStatus.Active);
    }
}

public class Flip7GameRulesTests
{
    [Fact]
    public void Turn_order_starts_after_the_dealer_and_dealer_acts_last()
    {
        var seats = new[] { "P0", "P1", "P2", "P3" };
        Flip7GameRules.TurnOrderFromDealer(seats, dealerSeat: 1)
            .Should().Equal("P2", "P3", "P0", "P1");
    }

    [Fact]
    public void Turn_order_wraps_for_last_seat_dealer()
    {
        var seats = new[] { "P0", "P1", "P2" };
        Flip7GameRules.TurnOrderFromDealer(seats, dealerSeat: 2)
            .Should().Equal("P0", "P1", "P2");
    }

    [Fact]
    public void Solo_turn_order_is_just_the_one_player()
    {
        Flip7GameRules.TurnOrderFromDealer(new[] { "solo" }, dealerSeat: 0)
            .Should().Equal("solo");
    }

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 0)]
    [InlineData(1, 1, 0)]
    public void Dealer_rotates_one_seat_each_round(int dealer, int seats, int expected)
    {
        Flip7GameRules.NextDealerSeat(dealer, seats).Should().Be(expected);
    }

    [Fact]
    public void Game_is_over_once_any_score_reaches_the_target()
    {
        Flip7GameRules.IsGameOver(new[] { 120, 205, 90 }, 200).Should().BeTrue();
        Flip7GameRules.IsGameOver(new[] { 120, 199, 90 }, 200).Should().BeFalse();
    }
}
