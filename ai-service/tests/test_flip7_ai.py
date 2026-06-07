"""Tests for the Flip 7 AI: bust-probability maths, the EV fallback decision
across styles/difficulty, request validation, and the Claude path (mocked)."""

import asyncio
import random
from types import SimpleNamespace
from unittest.mock import patch

import pytest
from pydantic import ValidationError

from game_models import Flip7AIMoveRequest, Flip7Opponent
from flip7_ai import bust_probability, expected_number_gain, greedy_decision


def _req(my_numbers=None, deck=None, draw=None, style="balanced", difficulty="medium",
         second_chance=False, modifiers=None, round_score=0, cumulative=0, opponents=None):
    my_numbers = my_numbers if my_numbers is not None else [3, 7]
    # default remaining deck: full-ish skew (value -> copies), large pile
    deck = deck if deck is not None else {v: v for v in range(1, 13)}
    draw = draw if draw is not None else sum(deck.values())
    return Flip7AIMoveRequest(
        myNumbers=my_numbers,
        myModifiers=modifiers or [],
        hasSecondChance=second_chance,
        myRoundScore=round_score if round_score else sum(my_numbers),
        myCumulativeScore=cumulative,
        deckRemaining=deck,
        drawPileCount=draw,
        opponents=opponents or [],
        style=style,
        difficulty=difficulty,
    )


def run(coro):
    return asyncio.new_event_loop().run_until_complete(coro)


# ── Probability core ───────────────────────────────────────────────────────────

class TestBustProbability:
    def test_zero_when_draw_pile_empty(self):
        assert bust_probability(_req(draw=0)) == 0.0

    def test_zero_with_second_chance(self):
        assert bust_probability(_req(my_numbers=[12], second_chance=True)) == 0.0

    def test_counts_duplicates_over_total(self):
        # hold 10 and 12; remaining has ten 10s + twelve 12s out of 50 cards
        req = _req(my_numbers=[10, 12], deck={10: 10, 12: 12, 5: 5}, draw=50)
        assert bust_probability(req) == pytest.approx((10 + 12) / 50)

    def test_zero_when_no_held_values_remain(self):
        req = _req(my_numbers=[3], deck={4: 4, 5: 5}, draw=9)
        assert bust_probability(req) == 0.0

    def test_expected_gain_excludes_busting_values(self):
        req = _req(my_numbers=[5], deck={5: 5, 10: 10}, draw=15)
        # only the 10s count toward gain: 10*10 / 10
        assert expected_number_gain(req) == pytest.approx(10.0)


# ── Fallback decision ──────────────────────────────────────────────────────────

class TestGreedyDecision:
    def test_must_hit_with_no_cards(self):
        assert greedy_decision(_req(my_numbers=[], modifiers=[])) == "hit"

    def test_hit_when_holding_second_chance(self):
        assert greedy_decision(_req(my_numbers=[5, 9], second_chance=True)) == "hit"

    def test_stay_when_bust_probability_high(self):
        # almost every remaining card is a duplicate
        req = _req(my_numbers=[12], deck={12: 9}, draw=10)
        assert greedy_decision(req) == "stay"

    def test_hit_when_bust_probability_low(self):
        req = _req(my_numbers=[2], deck={2: 1, 8: 8, 9: 9}, draw=40)
        assert greedy_decision(req) == "hit"

    def test_chases_flip7_at_six_unique_numbers(self):
        # 6 unique numbers, moderate bust risk → still hit for the +15 bonus
        req = _req(my_numbers=[1, 2, 3, 4, 5, 6], deck={1: 1, 2: 2, 3: 3, 12: 12}, draw=30)
        assert greedy_decision(req) == "hit"

    def test_stays_at_six_unique_when_bust_near_certain(self):
        req = _req(my_numbers=[1, 2, 3, 4, 5, 6], deck={1: 1, 2: 2, 3: 3}, draw=6)
        assert greedy_decision(req) == "stay"

    def test_risky_hits_where_safe_stays(self):
        # tuned so the bust prob lands between the safe and risky thresholds
        deck = {7: 4, 8: 8, 9: 9}
        req_common = dict(my_numbers=[7], deck=deck, draw=10, round_score=7)
        safe = greedy_decision(_req(style="safe", **req_common))
        risky = greedy_decision(_req(style="risky", **req_common))
        assert safe == "stay"
        assert risky == "hit"

    def test_easy_difficulty_can_deviate(self):
        # With a forced rng, the easy branch flips the normal decision.
        req = _req(my_numbers=[12], deck={12: 9}, draw=10, difficulty="easy")
        normal = "stay"  # high bust prob
        forced = greedy_decision(req, rng=random.Random(0))
        # over a few seeds at least one deviates from the strict line
        deviations = sum(
            greedy_decision(req, rng=random.Random(s)) != normal for s in range(20)
        )
        assert deviations > 0


# ── Request validation ─────────────────────────────────────────────────────────

class TestRequestModel:
    def test_defaults(self):
        req = Flip7AIMoveRequest(myNumbers=[3], drawPileCount=10)
        assert req.style == "balanced"
        assert req.difficulty == "medium"
        assert req.hasSecondChance is False

    @pytest.mark.parametrize("field,value", [("style", "wild"), ("difficulty", "godlike")])
    def test_rejects_invalid_tuning(self, field, value):
        with pytest.raises(ValidationError):
            Flip7AIMoveRequest(myNumbers=[3], drawPileCount=10, **{field: value})


# ── Claude path (mocked) ────────────────────────────────────────────────────────

class TestClaudePath:
    def _tool_block(self, action):
        return SimpleNamespace(type="tool_use", name="flip7_decision", input={"action": action})

    def test_uses_claude_decision_when_valid(self):
        from flip7_ai import get_flip7_move
        resp = SimpleNamespace(content=[self._tool_block("stay")])
        with patch("flip7_ai._client") as client:
            client.messages.create.return_value = resp
            out = run(get_flip7_move(_req(my_numbers=[5, 9])))
        assert out.action == "stay"
        assert out.source == "claude"

    def test_falls_back_when_claude_errors(self):
        from flip7_ai import get_flip7_move
        with patch("flip7_ai._client") as client:
            client.messages.create.side_effect = RuntimeError("boom")
            out = run(get_flip7_move(_req(my_numbers=[2], deck={2: 1, 8: 8}, draw=40)))
        assert out.source == "fallback"
        assert out.action in ("hit", "stay")

    def test_rejects_illegal_stay_from_claude(self):
        # Claude says "stay" but the player has no cards → fall back (which hits).
        from flip7_ai import get_flip7_move
        resp = SimpleNamespace(content=[self._tool_block("stay")])
        with patch("flip7_ai._client") as client:
            client.messages.create.return_value = resp
            out = run(get_flip7_move(_req(my_numbers=[], modifiers=[])))
        assert out.action == "hit"
        assert out.source == "fallback"
