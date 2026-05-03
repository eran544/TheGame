"""
Tests for message_validator — Claude-based chat validation and regex fallback.
The Anthropic client is fully mocked so no real API calls are made.
"""

import asyncio
from types import SimpleNamespace
from unittest.mock import patch

import pytest


def _tool_block(is_allowed: bool, reason: str = "") -> SimpleNamespace:
    return SimpleNamespace(
        type="tool_use",
        name="validation_result",
        input={"isAllowed": is_allowed, "reason": reason},
    )


def _response(*blocks) -> SimpleNamespace:
    return SimpleNamespace(content=list(blocks))


def run(coro):
    return asyncio.get_event_loop().run_until_complete(coro)


# ── Claude allows the message ──────────────────────────────────────────────

class TestClaudeAllows:
    def test_returns_true_for_allowed_message(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(_tool_block(True, ""))
            is_allowed, reason = run(validate_message("I'm in trouble"))

        assert is_allowed is True
        assert reason == ""

    def test_encouragement_is_allowed(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(_tool_block(True, ""))
            is_allowed, _ = run(validate_message("Great move! Let's keep going"))

        assert is_allowed is True

    def test_vague_pile_hint_is_allowed(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(_tool_block(True, ""))
            is_allowed, _ = run(validate_message("I can help the ascending pile"))

        assert is_allowed is True

    def test_dont_touch_pile_is_allowed(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(_tool_block(True, ""))
            is_allowed, _ = run(validate_message("Don't touch the first ascending pile"))

        assert is_allowed is True


# ── Claude blocks the message ──────────────────────────────────────────────

class TestClaudeBlocks:
    def test_returns_false_for_card_reveal(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(
                _tool_block(False, "Message reveals a specific card value (47)")
            )
            is_allowed, reason = run(validate_message("I have a 47"))

        assert is_allowed is False
        assert "47" in reason or reason != ""

    def test_returns_false_for_pile_value_reveal(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(
                _tool_block(False, "Message reveals a pile value")
            )
            is_allowed, reason = run(validate_message("The ascending pile is at 34"))

        assert is_allowed is False
        assert reason != ""

    def test_reason_is_returned_when_blocked(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = _response(
                _tool_block(False, "Reveals card number")
            )
            _, reason = run(validate_message("I'm holding 85"))

        assert reason == "Reveals card number"


# ── Fallback to regex when Claude fails ───────────────────────────────────

class TestRegexFallback:
    def test_falls_back_when_claude_raises(self):
        from message_validator import validate_message

        with patch("message_validator._client") as mock:
            mock.messages.create.side_effect = RuntimeError("timeout")
            is_allowed, _ = run(validate_message("things look good"))

        # "things look good" has no suspicious number pattern → allowed
        assert is_allowed is True

    def test_regex_blocks_suspicious_number_with_card_keyword(self):
        from message_validator import _regex_fallback

        is_allowed, reason = _regex_fallback("I have a 47 card")
        assert is_allowed is False
        assert reason != ""

    def test_regex_blocks_number_near_hold(self):
        from message_validator import _regex_fallback

        is_allowed, _ = _regex_fallback("I'm holding 85")
        assert is_allowed is False

    def test_regex_allows_vague_message(self):
        from message_validator import _regex_fallback

        is_allowed, reason = _regex_fallback("I can help the ascending pile")
        assert is_allowed is True
        assert reason == ""

    def test_regex_allows_encouragement(self):
        from message_validator import _regex_fallback

        is_allowed, _ = _regex_fallback("Great move! We're doing well")
        assert is_allowed is True

    def test_regex_allows_number_in_non_card_context(self):
        from message_validator import _regex_fallback

        # "have" is excluded from keywords; "3 players" has no card-context word
        is_allowed, _ = _regex_fallback("we have 3 players left")
        assert is_allowed is True

    def test_falls_back_when_no_tool_use_block(self):
        from message_validator import validate_message

        empty_response = SimpleNamespace(content=[])
        with patch("message_validator._client") as mock:
            mock.messages.create.return_value = empty_response
            # Falls back to regex; "I need that pile" is safe
            is_allowed, _ = run(validate_message("I need that pile"))

        assert is_allowed is True


# ── FastAPI endpoint ───────────────────────────────────────────────────────

class TestValidateMessageEndpoint:
    def test_endpoint_returns_200_for_allowed(self):
        from fastapi.testclient import TestClient
        from unittest.mock import AsyncMock, patch as apatch

        from main import app

        client = TestClient(app)
        with apatch("main._validate_chat", new=AsyncMock(return_value=(True, ""))):
            resp = client.post("/validate-message", json={"message": "I'm in trouble"})

        assert resp.status_code == 200
        assert resp.json()["isAllowed"] is True

    def test_endpoint_returns_200_for_blocked(self):
        from fastapi.testclient import TestClient
        from unittest.mock import AsyncMock, patch as apatch

        from main import app

        client = TestClient(app)
        with apatch("main._validate_chat", new=AsyncMock(return_value=(False, "Reveals card value"))):
            resp = client.post("/validate-message", json={"message": "I have a 47"})

        assert resp.status_code == 200
        assert resp.json()["isAllowed"] is False
        assert resp.json()["reason"] == "Reveals card value"

    def test_endpoint_rejects_missing_message(self):
        from fastapi.testclient import TestClient
        from main import app

        client = TestClient(app)
        resp = client.post("/validate-message", json={})
        assert resp.status_code == 422

    def test_endpoint_requires_message_field(self):
        from fastapi.testclient import TestClient
        from main import app

        client = TestClient(app)
        resp = client.post("/validate-message", json={"text": "wrong field"})
        assert resp.status_code == 422
