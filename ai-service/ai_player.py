import json
import logging
from typing import Any

import anthropic

from config import config
from game_models import AIMoveRequest, AIMoveResponse, AIMessageRequest, AIMessageResponse, CardPlay
from game_rules import greedy_fallback, validate_plays

logger = logging.getLogger(__name__)

_client = anthropic.Anthropic(api_key=config.ANTHROPIC_API_KEY)

_SYSTEM_PROMPT = """\
You are an AI player in The Game — a fully cooperative card game where all players \
win together or lose together. Your sole purpose is to help the ENTIRE TEAM succeed, \
not to play well for yourself alone.

RULES:
- 4 piles: ascending1 and ascending2 (start at 1, must go higher each play) and \
descending1 and descending2 (start at 100, must go lower each play).
- Cards range from 2 to 99. Each card appears only once.
- On your turn you MUST play at least minCardsThisTurn cards.
- BACKWARDS TRICK: on an ascending pile you may play a card that is EXACTLY 10 LESS \
than the current top (e.g. pile=45 → play 35). On a descending pile you may play a card \
EXACTLY 10 MORE than the current top (e.g. pile=60 → play 70). Use these aggressively — \
they give the whole team breathing room.
- When the draw pile is empty every player can play one card per turn. \
use this to allow other players to place their cards as well

GAME MEMORY:
- playedCards lists every card that has already been placed on a pile this game. \
Since each value exists only once, none of these cards can ever appear again — use this list \
to know exactly which values are permanently gone.
- moveHistory lists every move played this game in order. \
Use it to track each teammate's playing style, which piles they favour, and which ranges \
they are likely still holding.

COOPERATIVE STRATEGY:
- Study moveHistory to infer what cards teammates still hold and which piles they need.
- Avoid pushing a pile past a value a teammate likely needs.
- A backwards trick that costs you nothing is almost always worth taking.
- If two plays are equally cheap, choose the one that leaves the pile most accessible to teammates.
- When a backwards trick is available, first play every card in your hand that falls between \
the current pile top and the trick card, then play the backwards trick last. \
Example: ascending pile is at 38 and you hold 35, 41, 43, 45 — play 41 → 43 → 45, \
then close with the backwards trick 35. This extracts maximum value from the trick before resetting.
- When playing a card, always check whether adjacent cards in your hand extend the same run — \
if so, play them all together. Example: descending pile is at 58, you hold 54 and 53 — \
if 54 is your best play, immediately follow with 53. \
CRITICAL: every card value exists exactly once in the entire game. \
No teammate will ever hold the card you are skipping. \
Stranding a card you could have chained may permanently block a pile for the whole team — \
playing consecutive cards together is not optional, it is essential.
- Avoid pushing a pile past a value you still hold in your hand. \
Since each number exists only once, jumping over one of your own cards makes it unplayable forever.
- When the draw pile is empty, the minimum drops to one card per turn. \
Use this strategically — play fewer cards when a teammate needs a pile more urgently than you do.
- If you have no backwards trick and no run to extend, play exactly minCardsThisTurn and stop. \
Holding cards for future turns is almost always better than over-committing to a pile.

Always call the play_cards tool to submit your move.\
"""

_PLAY_CARDS_TOOL: dict[str, Any] = {
    "name": "play_cards",
    "description": (
        "Submit the cards you want to play this turn. "
        "You must play at least minCardsThisTurn cards."
    ),
    "input_schema": {
        "type": "object",
        "properties": {
            "plays": {
                "type": "array",
                "description": "Ordered list of cards to play.",
                "items": {
                    "type": "object",
                    "properties": {
                        "card": {"type": "integer", "description": "Card value (2–99)"},
                        "pileSlot": {
                            "type": "integer",
                            "description": "0=ascending1  1=ascending2  2=descending1  3=descending2",
                            "enum": [0, 1, 2, 3],
                        },
                    },
                    "required": ["card", "pileSlot"],
                },
                "minItems": 1,
            }
        },
        "required": ["plays"],
    },
}


def _build_user_message(req: AIMoveRequest) -> str:
    payload: dict[str, Any] = {
        "hand": sorted(req.hand),
        "piles": {
            "ascending1": req.piles.ascending1,
            "ascending2": req.piles.ascending2,
            "descending1": req.piles.descending1,
            "descending2": req.piles.descending2,
        },
        "drawPileCount": req.drawPileCount,
        "minCardsThisTurn": req.minCardsThisTurn,
    }
    if req.playedCards is not None:
        payload["playedCards"] = sorted(req.playedCards)
    if req.moveHistory:
        payload["moveHistory"] = [
            {
                "player": m.playerUsername,
                "plays": [{"card": p.card, "pileSlot": p.pileSlot} for p in m.plays],
            }
            for m in req.moveHistory
        ]
    return json.dumps(payload)


async def get_ai_move(req: AIMoveRequest) -> AIMoveResponse:
    """Request a move from Claude; fall back to greedy algorithm on any failure."""
    try:
        response = _client.messages.create(
            model=config.ANTHROPIC_MODEL,
            max_tokens=256,
            system=[
                {
                    "type": "text",
                    "text": _SYSTEM_PROMPT,
                    "cache_control": {"type": "ephemeral"},
                }
            ],
            tools=[_PLAY_CARDS_TOOL],
            tool_choice={"type": "any"},
            messages=[{"role": "user", "content": _build_user_message(req)}],
        )

        for block in response.content:
            if block.type == "tool_use" and block.name == "play_cards":
                plays = [CardPlay(**p) for p in block.input["plays"]]
                if len(plays) >= req.minCardsThisTurn and validate_plays(
                    plays, req.hand, req.piles
                ):
                    logger.info("Claude chose %d play(s)", len(plays))
                    return AIMoveResponse(plays=plays, source="claude")
                logger.warning("Claude returned invalid plays — using greedy fallback")

    except Exception:
        logger.exception("Claude API error — using greedy fallback")

    plays = greedy_fallback(
        req.hand, req.piles, req.minCardsThisTurn, req.drawPileCount
    )
    logger.info("Greedy fallback chose %d play(s)", len(plays))
    return AIMoveResponse(plays=plays, source="fallback")


_MESSAGE_SYSTEM_PROMPT = """\
You are an AI player in The Game — a fully cooperative card game. Generate a single short \
chat message (under 15 words) to send to your teammates. You MUST NOT reveal specific card \
or pile values. Follow these communication rules strictly:

ALLOWED:
- General encouragement or status: "We've got this!", "I'm in a tight spot"
- Vague strategic hints: "Things are looking up", "I think I just helped us"
- Negotiating pile access without numbers: "Please leave the first ascending pile for me if \
  you can", "I really need the second descending pile", "Can someone else handle the \
  ascending piles? I'll focus on descending"
- Responding to a teammate's prior request: "Got it, I'll stay off that pile", \
  "I'll try to leave that one for you"
- Apologising when forced to override a teammate's request: \
  "Sorry, I had no choice but to play there", "Apologies — I was completely forced to touch \
  that pile", "Had to play there, no other option — sorry"

NOT ALLOWED:
- Specific numbers: "I have a 47", "The pile is at 62", "I need to play 35"
- Any phrasing that directly reveals a card value

CONTEXT CUES — look at the move history to decide what to say:
- If a teammate recently played heavily on a pile you also need, consider negotiating
- If you just took a pile a teammate likely needs, apologise
- If you used a backwards trick, you can hint that you helped without stating the value
- If the draw pile is empty or nearly empty, signal urgency
- If you are about to run out of good moves, warn the team vaguely
- If a teammate's last message (in history) asked for something, acknowledge it

Generate ONLY the message text, no quotes, no extra text.\
"""


_FALLBACK_MESSAGES = [
    "We've got this!",
    "I'm in a tight spot...",
    "Things are looking up!",
    "Trust the process, team!",
    "Hmm, need to think carefully here.",
    "I can help with the ascending piles!",
    "Please leave the second ascending pile for me if possible.",
    "I really need one of the descending piles.",
    "Can someone else cover the ascending side? I'll take descending.",
    "Hang in there everyone!",
    "Sorry, had to touch that pile — absolutely no other option.",
    "Apologies, I was forced to play there.",
    "Had to play there, no other option — sorry!",
    "I think I just gave us a bit of breathing room.",
    "That was tight but we're still in it.",
    "Feeling okay about where we are.",
    "I'm running low on good options, heads up.",
    "Getting tricky over here — watch those piles.",
    "Don't worry about that pile for now, I've got it.",
    "Can anyone cover the first ascending pile? I need to focus elsewhere.",
]


async def get_ai_message(req: AIMessageRequest) -> AIMessageResponse:
    """Generate a cooperative chat message for an AI player."""
    import random

    piles_summary = (
        f"ascending piles: {req.piles.ascending1} and {req.piles.ascending2}, "
        f"descending piles: {req.piles.descending1} and {req.piles.descending2}, "
        f"draw pile: {req.drawPileCount} cards, "
        f"my hand: {len(req.hand)} cards"
    )

    history_summary = ""
    if req.moveHistory:
        last_moves = req.moveHistory[-4:]
        history_summary = " Recent moves: " + "; ".join(
            f"{m.playerUsername} played {len(m.plays)} card(s) on "
            + ", ".join(
                ["asc1", "asc2", "desc1", "desc2"][p.pileSlot]
                for p in m.plays
            )
            for m in last_moves
        )

    try:
        response = _client.messages.create(
            model=config.ANTHROPIC_MODEL,
            max_tokens=64,
            system=[
                {
                    "type": "text",
                    "text": _MESSAGE_SYSTEM_PROMPT,
                    "cache_control": {"type": "ephemeral"},
                }
            ],
            messages=[
                {
                    "role": "user",
                    "content": (
                        f"I am {req.playerUsername}. "
                        f"State: {piles_summary}.{history_summary} "
                        "What should I say to my teammates?"
                    ),
                }
            ],
        )

        for block in response.content:
            if hasattr(block, "text") and block.text:
                message = block.text.strip().strip('"').strip("'")
                if message:
                    logger.info("AI message generated by Claude")
                    return AIMessageResponse(message=message)

    except Exception:
        logger.exception("Claude API error generating message — using fallback")

    return AIMessageResponse(message=random.choice(_FALLBACK_MESSAGES))
