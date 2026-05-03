"""
Integration tests for the FastAPI /ai-move endpoint.
The underlying AI logic is mocked so no real Claude calls are made.
"""

from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest
from fastapi.testclient import TestClient

from game_models import AIMoveResponse, CardPlay


@pytest.fixture
def client():
    from main import app
    return TestClient(app)


def _move_response(plays=None, source="claude") -> AIMoveResponse:
    return AIMoveResponse(
        plays=plays or [CardPlay(card=2, pileSlot=0), CardPlay(card=3, pileSlot=1)],
        source=source,
    )


def _valid_body(**overrides):
    base = {
        "hand": [2, 3, 10, 20, 50, 80, 95],
        "piles": {
            "ascending1": 1,
            "ascending2": 1,
            "descending1": 100,
            "descending2": 100,
        },
        "drawPileCount": 40,
        "minCardsThisTurn": 2,
    }
    base.update(overrides)
    return base


# ── Health / root ──────────────────────────────────────────────────────────

class TestInfraEndpoints:
    def test_root_returns_200(self, client):
        resp = client.get("/")
        assert resp.status_code == 200
        assert resp.json()["status"] == "running"

    def test_health_returns_200_and_healthy(self, client):
        resp = client.get("/health")
        assert resp.status_code == 200
        assert resp.json()["status"] == "healthy"

    def test_health_includes_timestamp(self, client):
        resp = client.get("/health")
        assert "timestamp" in resp.json()


# ── POST /ai-move ──────────────────────────────────────────────────────────

class TestAIMoveEndpoint:
    def test_returns_200_with_valid_request(self, client):
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=_valid_body())
        assert resp.status_code == 200

    def test_response_contains_plays_array(self, client):
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=_valid_body())
        assert "plays" in resp.json()
        assert isinstance(resp.json()["plays"], list)

    def test_response_contains_source_field(self, client):
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=_valid_body())
        assert "source" in resp.json()

    def test_response_source_claude(self, client):
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response(source="claude"))):
            resp = client.post("/ai-move", json=_valid_body())
        assert resp.json()["source"] == "claude"

    def test_response_source_fallback(self, client):
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response(source="fallback"))):
            resp = client.post("/ai-move", json=_valid_body())
        assert resp.json()["source"] == "fallback"

    def test_play_contains_card_and_pile_slot(self, client):
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=_valid_body())
        play = resp.json()["plays"][0]
        assert "card" in play
        assert "pileSlot" in play

    def test_accepts_optional_move_history(self, client):
        body = _valid_body(moveHistory=[{
            "playerUsername": "alice",
            "plays": [{"card": 5, "pileSlot": 0}],
        }])
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=body)
        assert resp.status_code == 200

    def test_accepts_optional_played_cards(self, client):
        body = _valid_body(playedCards=[5, 12, 23, 41])
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=body)
        assert resp.status_code == 200

    def test_accepts_missing_optional_fields(self, client):
        body = _valid_body()
        with patch("main.get_ai_move", new=AsyncMock(return_value=_move_response())):
            resp = client.post("/ai-move", json=body)
        assert resp.status_code == 200


# ── Validation: malformed requests ────────────────────────────────────────

class TestAIMoveValidation:
    def test_missing_hand_returns_422(self, client):
        body = _valid_body()
        del body["hand"]
        resp = client.post("/ai-move", json=body)
        assert resp.status_code == 422

    def test_missing_piles_returns_422(self, client):
        body = _valid_body()
        del body["piles"]
        resp = client.post("/ai-move", json=body)
        assert resp.status_code == 422

    def test_missing_draw_pile_count_returns_422(self, client):
        body = _valid_body()
        del body["drawPileCount"]
        resp = client.post("/ai-move", json=body)
        assert resp.status_code == 422

    def test_missing_min_cards_this_turn_returns_422(self, client):
        body = _valid_body()
        del body["minCardsThisTurn"]
        resp = client.post("/ai-move", json=body)
        assert resp.status_code == 422

    def test_partial_piles_object_returns_422(self, client):
        body = _valid_body()
        body["piles"] = {"ascending1": 1}  # missing 3 fields
        resp = client.post("/ai-move", json=body)
        assert resp.status_code == 422

    def test_non_list_hand_returns_422(self, client):
        body = _valid_body()
        body["hand"] = "not-a-list"
        resp = client.post("/ai-move", json=body)
        assert resp.status_code == 422
