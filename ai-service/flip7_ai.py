"""Flip 7 AI: press-your-luck Hit/Stay decisions.

The deck is deliberately skewed (higher numbers appear more often), so the core
decision is a bust-probability / expected-value trade-off over the cards that can
still be drawn. Claude is the primary decision-maker; a deterministic EV model is
the fallback and the source of truth for the maths.
"""

import json
import logging
import random
from typing import Any

import anthropic

from config import config
from game_models import Flip7AIMoveRequest, Flip7AIMoveResponse

logger = logging.getLogger(__name__)

_client = anthropic.Anthropic(api_key=config.ANTHROPIC_API_KEY)

UNIQUE_NUMBERS_FOR_FLIP7 = 7
FLIP7_BONUS = 15


# ── Probability / EV core (deterministic — also drives the fallback) ───────────

def bust_probability(req: Flip7AIMoveRequest) -> float:
    """Probability the next single card busts me: drawing a duplicate of a value
    I already hold. A held Second Chance absorbs the first duplicate, so the
    immediate hit cannot bust — that risk is effectively zero for this decision."""
    if req.drawPileCount <= 0:
        return 0.0
    if req.hasSecondChance:
        return 0.0
    duplicates = sum(req.deckRemaining.get(v, 0) for v in req.myNumbers)
    return min(max(duplicates / req.drawPileCount, 0.0), 1.0)


def expected_number_gain(req: Flip7AIMoveRequest) -> float:
    """Expected points added by the next card, conditional on it being a
    non-busting number card (modifiers/actions are treated as ~0 for this
    estimate). Used to weigh the upside of hitting against the bust risk."""
    safe_value_total = 0.0
    safe_count = 0
    for value, count in req.deckRemaining.items():
        if value in req.myNumbers:
            continue  # would bust, not a gain
        safe_value_total += value * count
        safe_count += count
    return safe_value_total / safe_count if safe_count else 0.0


# Risk appetite: the bust probability at/above which a "balanced" player stays.
_STYLE_THRESHOLD = {"safe": 0.28, "balanced": 0.42, "risky": 0.58}


def greedy_decision(req: Flip7AIMoveRequest, rng: random.Random | None = None) -> str:
    """Deterministic (per difficulty) Hit/Stay decision. ``easy`` injects some
    sub-optimal noise via ``rng``; ``hard`` plays the EV line tightly."""
    rng = rng or random
    can_stay = len(req.myNumbers) > 0 or len(req.myModifiers) > 0

    # Must take a card if there's nothing to bank yet.
    if not can_stay:
        return "hit"

    # Holding a Second Chance, this hit can't bust — keep building.
    if req.hasSecondChance:
        return "hit"

    p_bust = bust_probability(req)

    # One card away from Flip 7: the +15 bonus (and round-ending swing) is worth
    # chasing unless a bust is near-certain.
    if len(req.myNumbers) == UNIQUE_NUMBERS_FOR_FLIP7 - 1:
        return "hit" if p_bust < 0.85 else "stay"

    threshold = _STYLE_THRESHOLD.get(req.style, _STYLE_THRESHOLD["balanced"])

    # Protect a large banked line a little more carefully; gamble more when the
    # line is still cheap to lose.
    if req.myRoundScore >= 25:
        threshold -= 0.08
    elif req.myRoundScore <= 5:
        threshold += 0.08

    # Trailing the leader by a lot? Push harder for points.
    leader = max((o.cumulativeScore for o in req.opponents), default=0)
    if leader - req.myCumulativeScore >= 40:
        threshold += 0.06

    if req.difficulty == "easy" and rng.random() < 0.25:
        # Beginner: occasionally make the wrong call.
        return "stay" if p_bust < threshold else "hit"

    return "stay" if p_bust >= threshold else "hit"


# ── Claude prompt ─────────────────────────────────────────────────────────────

_SYSTEM_PROMPT = """\
You are an AI player in Flip 7, a press-your-luck card game. Each round you build a \
line of face-up cards. On your turn you choose:
- HIT: draw one more card.
- STAY: stop and bank your current points for the round.

KEY RULES:
- Number cards (0–12) score their value. The deck is SKEWED: higher numbers have more \
copies (twelve 12s, eleven 11s, … one 1, one 0), so high values are both more valuable \
and more likely to be drawn — and therefore more dangerous as duplicates.
- BUST: if you draw a number value already in your line, you lose ALL points for the round.
- FLIP 7: collecting 7 unique number cards ends the round immediately and adds +15 points.
- A held SECOND CHANCE card absorbs your next bust (so the next hit cannot bust you).
- Modifier cards (+2..+10, x2) and action cards never bust you.
- First player to 200 points across rounds wins.

DECISION GUIDANCE:
- Weigh the bust probability of the next card (copies of your held values still in the \
deck, divided by cards remaining) against the points you'd lose by busting.
- The more unique numbers you already hold, the higher the bust risk — but at 6 unique \
numbers, chasing the 7th for the Flip 7 bonus is usually worth a real gamble.
- If you hold a Second Chance, hitting is almost always correct.
- Banking a large line is worth protecting; a small line is cheap to risk.

Always call the flip7_decision tool with your choice.\
"""

_STYLE_GUIDANCE = {
    "safe": (
        "Play it SAFE: bank early, keep bust risk low, and only chase the Flip 7 bonus when "
        "the odds are clearly in your favour."
    ),
    "balanced": (
        "Play a BALANCED game: take reasonable risks, push for value, but don't gamble a big "
        "line on a coin flip."
    ),
    "risky": (
        "Play AGGRESSIVELY: tolerate higher bust risk, chase high cards and the Flip 7 bonus, "
        "and keep hitting when there's upside."
    ),
}

_DIFFICULTY_GUIDANCE = {
    "easy": "You are a BEGINNER: make simple, intuitive calls and don't compute exact odds.",
    "medium": "You are a COMPETENT player: weigh risk and reward sensibly.",
    "hard": "You are an EXPERT: compute the bust odds precisely and play the optimal expected-value line.",
}


def _build_system_blocks(style: str, difficulty: str) -> list[dict[str, Any]]:
    style_text = _STYLE_GUIDANCE.get(style, _STYLE_GUIDANCE["balanced"])
    difficulty_text = _DIFFICULTY_GUIDANCE.get(difficulty, _DIFFICULTY_GUIDANCE["medium"])
    return [
        {"type": "text", "text": _SYSTEM_PROMPT, "cache_control": {"type": "ephemeral"}},
        {
            "type": "text",
            "text": f"PLAY STYLE:\n{style_text}\n\nSKILL LEVEL:\n{difficulty_text}",
            "cache_control": {"type": "ephemeral"},
        },
    ]


_DECISION_TOOL: dict[str, Any] = {
    "name": "flip7_decision",
    "description": "Submit your Hit or Stay decision for this turn.",
    "input_schema": {
        "type": "object",
        "properties": {
            "action": {
                "type": "string",
                "enum": ["hit", "stay"],
                "description": "hit = draw another card; stay = bank your points.",
            }
        },
        "required": ["action"],
    },
}


def _build_user_message(req: Flip7AIMoveRequest) -> str:
    payload = {
        "myNumbers": sorted(req.myNumbers),
        "myModifiers": req.myModifiers,
        "hasSecondChance": req.hasSecondChance,
        "myRoundScore": req.myRoundScore,
        "myCumulativeScore": req.myCumulativeScore,
        "targetScore": req.targetScore,
        "uniqueNumbers": len(req.myNumbers),
        "drawPileCount": req.drawPileCount,
        "bustProbability": round(bust_probability(req), 3),
        "expectedNumberGain": round(expected_number_gain(req), 2),
        "opponents": [
            {
                "numberCount": o.numberCount,
                "roundScore": o.roundScore,
                "status": o.status,
                "cumulativeScore": o.cumulativeScore,
            }
            for o in req.opponents
        ],
    }
    return json.dumps(payload)


async def get_flip7_move(req: Flip7AIMoveRequest) -> Flip7AIMoveResponse:
    """Ask Claude for a Hit/Stay decision; fall back to the EV model on any failure
    or if a 'stay' is returned while staying isn't legal (no cards yet)."""
    p_bust = bust_probability(req)
    can_stay = len(req.myNumbers) > 0 or len(req.myModifiers) > 0

    try:
        response = _client.messages.create(
            model=config.ANTHROPIC_MODEL,
            max_tokens=128,
            system=_build_system_blocks(req.style, req.difficulty),
            tools=[_DECISION_TOOL],
            tool_choice={"type": "any"},
            messages=[{"role": "user", "content": _build_user_message(req)}],
        )
        for block in response.content:
            if block.type == "tool_use" and block.name == "flip7_decision":
                action = block.input.get("action")
                if action in ("hit", "stay") and not (action == "stay" and not can_stay):
                    logger.info("Claude flip7 decision: %s", action)
                    return Flip7AIMoveResponse(action=action, source="claude", bustProbability=p_bust)
                logger.warning("Claude returned an unusable flip7 decision — using fallback")
    except Exception:
        logger.exception("Claude API error — using flip7 EV fallback")

    action = greedy_decision(req)
    logger.info("Flip7 EV fallback decision: %s", action)
    return Flip7AIMoveResponse(action=action, source="fallback", bustProbability=p_bust)
