using FluentAssertions;
using Flip7Server.Game;

namespace Flip7Server.Tests.Game;

public class Flip7ScoringTests
{
    [Fact]
    public void Sum_of_number_cards_only()
    {
        // Rulebook example: 3 + 11 + 5 + 7 + 10 = 36
        Flip7Scoring.Score(new[] { 3, 11, 5, 7, 10 }, Array.Empty<ModifierKind>(), false)
            .Should().Be(36);
    }

    [Fact]
    public void Times2_doubles_the_number_sum()
    {
        // 36 × 2 = 72
        Flip7Scoring.Score(new[] { 3, 11, 5, 7, 10 }, new[] { ModifierKind.Times2 }, false)
            .Should().Be(72);
    }

    [Fact]
    public void Additive_modifier_adds_after_sum()
    {
        // 36 + 10 = 46
        Flip7Scoring.Score(new[] { 3, 11, 5, 7, 10 }, new[] { ModifierKind.Plus10 }, false)
            .Should().Be(46);
    }

    [Fact]
    public void Flip7_adds_15_bonus()
    {
        // 3+11+5+7+10+9+4 = 49, +15 = 64
        Flip7Scoring.Score(new[] { 3, 11, 5, 7, 10, 9, 4 }, Array.Empty<ModifierKind>(), true)
            .Should().Be(64);
    }

    [Fact]
    public void Times2_is_applied_before_additive_modifiers()
    {
        // (3 + 5) = 8 → ×2 = 16 → +2 = 18  (NOT (8+2)×2 = 20)
        Flip7Scoring.Score(new[] { 3, 5 }, new[] { ModifierKind.Times2, ModifierKind.Plus2 }, false)
            .Should().Be(18);
    }

    [Fact]
    public void Multiple_additive_modifiers_stack()
    {
        // 10 + 2 + 4 + 6 = 22
        Flip7Scoring.Score(new[] { 10 }, new[] { ModifierKind.Plus2, ModifierKind.Plus4, ModifierKind.Plus6 }, false)
            .Should().Be(22);
    }

    [Fact]
    public void Times2_over_empty_line_yields_zero()
    {
        // Edge case: staying with only an x2 modifier multiplies 0 → 0.
        Flip7Scoring.Score(Array.Empty<int>(), new[] { ModifierKind.Times2 }, false)
            .Should().Be(0);
    }

    [Fact]
    public void Additive_modifier_over_empty_line_scores_the_modifier()
    {
        Flip7Scoring.Score(Array.Empty<int>(), new[] { ModifierKind.Plus6 }, false)
            .Should().Be(6);
    }

    [Fact]
    public void Zero_card_contributes_no_points_but_counts_as_a_card()
    {
        Flip7Scoring.Score(new[] { 0, 5 }, Array.Empty<ModifierKind>(), false).Should().Be(5);
    }
}
