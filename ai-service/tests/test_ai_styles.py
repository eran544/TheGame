"""
Tests for AI difficulty/style: request fields, prompt composition, and the
style-tuned greedy fallback. The Anthropic client is mocked — no real API calls.
"""

import asyncio
from types import SimpleNamespace
from unittest.mock import patch

import pytest
from pydantic import ValidationError

from game_models import AIMoveRequest, PilesState
from game_rules import greedy_fallback


def _request(hand=None, piles=None, draw_pile_count=40, min_cards=2, style="balanced", difficulty="medium"):
    return AIMoveRequest(
        hand=hand or [2, 3, 10, 20, 50, 80, 95],
        piles=piles or PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100),
        drawPileCount=draw_pile_count,
        minCardsThisTurn=min_cards,
        style=style,
        difficulty=difficulty,
    )


def _tool_block(plays):
    return SimpleNamespace(type="tool_use", name="play_cards", input={"plays": plays})


def run(coro):
    return asyncio.get_event_loop().run_until_complete(coro)


# ── Request model ─────────────────────────────────────────────────────────────

class TestRequestFields:
    def test_defaults(self):
        req = _request()
        # explicit defaults round-trip; also verify a request built without them
        req2 = AIMoveRequest(
            hand=[2, 3],
            piles=PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100),
            drawPileCount=10,
            minCardsThisTurn=1,
        )
        assert req2.difficulty == "medium"
        assert req2.style == "balanced"

    def test_accepts_valid_values(self):
        req = _request(style="risky", difficulty="hard")
        assert req.style == "risky"
        assert req.difficulty == "hard"

    @pytest.mark.parametrize("field,value", [("style", "reckless"), ("difficulty", "extreme")])
    def test_rejects_invalid_values(self, field, value):
        with pytest.raises(ValidationError):
            _request(**{field: value})


# ── Prompt composition ────────────────────────────────────────────────────────

class TestSystemPromptComposition:
    def test_returns_base_plus_variant_blocks(self):
        from ai_player import _build_system_blocks, _SYSTEM_PROMPT

        blocks = _build_system_blocks("risky", "hard")
        assert len(blocks) == 2
        assert blocks[0]["text"] == _SYSTEM_PROMPT
        assert all(b["cache_control"] == {"type": "ephemeral"} for b in blocks)

    def test_variant_reflects_style_and_difficulty(self):
        from ai_player import _build_system_blocks

        risky_hard = _build_system_blocks("risky", "hard")[1]["text"]
        safe_easy = _build_system_blocks("safe", "easy")[1]["text"]
        assert "AGGRESSIVELY" in risky_hard and "EXPERT" in risky_hard
        assert "SAFE" in safe_easy and "BEGINNER" in safe_easy
        assert risky_hard != safe_easy

    def test_unknown_values_fall_back_to_defaults(self):
        from ai_player import _build_system_blocks

        unknown = _build_system_blocks("bogus", "bogus")[1]["text"]
        balanced_medium = _build_system_blocks("balanced", "medium")[1]["text"]
        assert unknown == balanced_medium

    def test_get_ai_move_passes_styled_prompt(self):
        from ai_player import get_ai_move

        resp = SimpleNamespace(content=[_tool_block([
            {"card": 2, "pileSlot": 0}, {"card": 3, "pileSlot": 1},
        ])])
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = resp
            run(get_ai_move(_request(style="risky", difficulty="hard")))

        system = mock_client.messages.create.call_args.kwargs["system"]
        assert len(system) == 2
        assert "AGGRESSIVELY" in system[1]["text"]


# ── Style-tuned greedy fallback ───────────────────────────────────────────────

class TestStyledFallback:
    def test_default_is_balanced_and_backward_compatible(self):
        # Backwards trick available after the minimum: balanced takes it.
        piles = PilesState(ascending1=40, ascending2=1, descending1=100, descending2=100)
        no_style = greedy_fallback([41, 31], piles, 1, 40)
        balanced = greedy_fallback([41, 31], piles, 1, 40, "balanced")
        assert [p.card for p in no_style] == [p.card for p in balanced] == [41, 31]

    def test_safe_stops_at_minimum_even_with_backwards_trick(self):
        piles = PilesState(ascending1=40, ascending2=1, descending1=100, descending2=100)
        plays = greedy_fallback([41, 31], piles, 1, 40, "safe")
        assert [p.card for p in plays] == [41]

    def test_risky_takes_small_forward_jump_past_minimum(self):
        piles = PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100)
        balanced = greedy_fallback([2, 3], piles, 1, 40, "balanced")
        risky = greedy_fallback([2, 3], piles, 1, 40, "risky")
        assert [p.card for p in balanced] == [2]          # balanced stops
        assert [p.card for p in risky] == [2, 3]          # risky keeps momentum

    def test_empty_draw_pile_overrides_safe(self):
        # With draw pile empty everyone must dump as much as possible regardless of style.
        piles = PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100)
        plays = greedy_fallback([2, 3], piles, 1, 0, "safe")
        assert len(plays) == 2
