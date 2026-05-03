"""
Tests for ai_player.get_ai_move — Claude integration and greedy fallback.
The Anthropic client is fully mocked so no real API calls are made.
"""

import asyncio
from types import SimpleNamespace
from unittest.mock import MagicMock, patch

import pytest

from game_models import AIMoveRequest, CardPlay, LastMove, LastMovePlay, PilesState

# ── Helpers ───────────────────────────────────────────────────────────────────

def _tool_block(plays: list[dict]) -> SimpleNamespace:
    return SimpleNamespace(type="tool_use", name="play_cards", input={"plays": plays})


def _text_block(text: str = "thinking...") -> SimpleNamespace:
    return SimpleNamespace(type="text", text=text)


def _response(*blocks) -> SimpleNamespace:
    return SimpleNamespace(content=list(blocks))


def _request(
    hand=None,
    piles=None,
    draw_pile_count=40,
    min_cards=2,
    played_cards=None,
    move_history=None,
) -> AIMoveRequest:
    return AIMoveRequest(
        hand=hand or [2, 3, 10, 20, 50, 80, 95],
        piles=piles or PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100),
        drawPileCount=draw_pile_count,
        minCardsThisTurn=min_cards,
        playedCards=played_cards,
        moveHistory=move_history,
    )


def run(coro):
    return asyncio.get_event_loop().run_until_complete(coro)


# ── Valid Claude response ─────────────────────────────────────────────────────

class TestClaudeValidResponse:
    def test_uses_claude_plays_when_valid(self):
        from ai_player import get_ai_move

        mock_resp = _response(_tool_block([
            {"card": 2, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "claude"
        assert len(result.plays) == 2
        assert result.plays[0].card == 2
        assert result.plays[1].card == 3

    def test_source_is_claude_on_success(self):
        from ai_player import get_ai_move

        mock_resp = _response(_tool_block([
            {"card": 10, "pileSlot": 0},
            {"card": 20, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "claude"

    def test_passes_request_to_anthropic_client(self):
        from ai_player import get_ai_move

        mock_resp = _response(_tool_block([
            {"card": 2, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            run(get_ai_move(_request()))

        mock_client.messages.create.assert_called_once()
        call_kwargs = mock_client.messages.create.call_args.kwargs
        assert call_kwargs["tool_choice"] == {"type": "any"}
        assert call_kwargs["tools"][0]["name"] == "play_cards"

    def test_claude_response_with_preceding_text_block_still_parsed(self):
        from ai_player import get_ai_move

        mock_resp = _response(
            _text_block("Let me think about this..."),
            _tool_block([{"card": 2, "pileSlot": 0}, {"card": 3, "pileSlot": 1}]),
        )
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "claude"

    def test_move_history_included_in_request_when_provided(self):
        from ai_player import get_ai_move

        history = [LastMove(
            playerUsername="alice",
            plays=[LastMovePlay(card=5, pileSlot=0)],
        )]
        mock_resp = _response(_tool_block([
            {"card": 2, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            run(get_ai_move(_request(move_history=history)))

        import json
        call_kwargs = mock_client.messages.create.call_args.kwargs
        user_content = call_kwargs["messages"][0]["content"]
        payload = json.loads(user_content)
        assert "moveHistory" in payload
        assert payload["moveHistory"][0]["player"] == "alice"

    def test_played_cards_included_in_request_when_provided(self):
        from ai_player import get_ai_move

        mock_resp = _response(_tool_block([
            {"card": 2, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            run(get_ai_move(_request(played_cards=[5, 12, 23])))

        import json
        call_kwargs = mock_client.messages.create.call_args.kwargs
        user_content = call_kwargs["messages"][0]["content"]
        payload = json.loads(user_content)
        assert "playedCards" in payload
        assert payload["playedCards"] == [5, 12, 23]

    def test_played_cards_omitted_when_none(self):
        from ai_player import get_ai_move

        mock_resp = _response(_tool_block([
            {"card": 2, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            run(get_ai_move(_request()))

        import json
        call_kwargs = mock_client.messages.create.call_args.kwargs
        user_content = call_kwargs["messages"][0]["content"]
        payload = json.loads(user_content)
        assert "playedCards" not in payload


# ── Fallback: Claude returns invalid plays ───────────────────────────────────

class TestFallbackOnInvalidClaudePlays:
    def test_falls_back_when_claude_plays_too_few_cards(self):
        from ai_player import get_ai_move

        # min_cards=2 but Claude only plays 1
        mock_resp = _response(_tool_block([{"card": 2, "pileSlot": 0}]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request(min_cards=2)))

        assert result.source == "fallback"

    def test_falls_back_when_card_not_in_hand(self):
        from ai_player import get_ai_move

        # Card 99 is not in the hand [2, 3, 10, 20, 50, 80, 95]
        mock_resp = _response(_tool_block([
            {"card": 99, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "fallback"

    def test_falls_back_when_card_violates_pile_rule(self):
        from ai_player import get_ai_move

        piles = PilesState(ascending1=50, ascending2=1, descending1=100, descending2=100)
        # Card 30 < 50 on ascending1 and not 50-10=40 → invalid
        mock_resp = _response(_tool_block([
            {"card": 30, "pileSlot": 0},
            {"card": 3, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request(hand=[3, 30, 40], piles=piles)))

        assert result.source == "fallback"

    def test_falls_back_when_same_card_played_twice(self):
        from ai_player import get_ai_move

        mock_resp = _response(_tool_block([
            {"card": 2, "pileSlot": 0},
            {"card": 2, "pileSlot": 1},
        ]))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "fallback"

    def test_falls_back_when_no_tool_use_block_returned(self):
        from ai_player import get_ai_move

        mock_resp = _response(_text_block("I pass."))
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "fallback"

    def test_falls_back_when_content_list_is_empty(self):
        from ai_player import get_ai_move

        mock_resp = _response()  # empty content
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.return_value = mock_resp
            result = run(get_ai_move(_request()))

        assert result.source == "fallback"


# ── Fallback: Claude raises an exception ─────────────────────────────────────

class TestFallbackOnException:
    def test_falls_back_on_api_error(self):
        from ai_player import get_ai_move

        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.side_effect = RuntimeError("network error")
            result = run(get_ai_move(_request()))

        assert result.source == "fallback"

    def test_fallback_plays_are_valid(self):
        from ai_player import get_ai_move
        from game_rules import validate_plays

        req = _request()
        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.side_effect = RuntimeError("timeout")
            result = run(get_ai_move(req))

        assert result.source == "fallback"
        assert validate_plays(result.plays, req.hand, req.piles)
        assert len(result.plays) >= req.minCardsThisTurn

    def test_falls_back_on_connection_error(self):
        from ai_player import get_ai_move

        with patch("ai_player._client") as mock_client:
            mock_client.messages.create.side_effect = ConnectionError("refused")
            result = run(get_ai_move(_request()))

        assert result.source == "fallback"
