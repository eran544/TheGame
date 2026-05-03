import logging
import re

import anthropic

from config import config

logger = logging.getLogger(__name__)

_client = anthropic.Anthropic(api_key=config.ANTHROPIC_API_KEY)

_SYSTEM_PROMPT = """\
You are enforcing communication rules for The Game — a cooperative card game.

Players CANNOT reveal specific card values or pile values in their messages.
Players CAN use general encouragement and vague strategic hints.

FORBIDDEN (block these):
- Specific card numbers: "I have a 47", "my lowest is 23", "I'm holding 85"
- Pile state: "the pile is at 34", "it's near 60", "the ascending pile is around 50"
- Directional hints with numbers: "play below 40", "we need something around 70"
- Any phrasing that narrows down which card or pile value someone holds

ALLOWED (permit these):
- "I'm in trouble" / "things look good" / "we're doing well"
- "I can help the ascending pile" / "I have something useful for descending"
- "I need that pile" / "don't touch the first ascending pile"
- "trust me on this" / "I can fix that pile"
- General encouragement: "great move!" / "let's keep going"
- Vague strategy: "let's focus on ascending piles" / "be careful with descending"

A message is allowed if it coordinates strategy WITHOUT revealing specific values.\
"""

_VALIDATE_TOOL = {
    "name": "validation_result",
    "description": "Return whether the message follows the communication rules.",
    "input_schema": {
        "type": "object",
        "properties": {
            "isAllowed": {
                "type": "boolean",
                "description": "True if the message is allowed; false if it reveals forbidden information",
            },
            "reason": {
                "type": "string",
                "description": "Brief explanation if blocked; empty string if allowed",
            },
        },
        "required": ["isAllowed", "reason"],
    },
}

# Fallback: number (2-99) near suspicious keywords, in either order.
# "have" is intentionally excluded — too generic ("we have 3 players").
_SUSPICIOUS = re.compile(
    r'(?:'
    r'\b(?:[2-9]|[1-9][0-9])\b.{0,30}(?:card|hold|holding|pile|value|number|below|above|around)'
    r'|'
    r'(?:card|hold|holding|pile|value|number|below|above|around).{0,30}\b(?:[2-9]|[1-9][0-9])\b'
    r')',
    re.IGNORECASE,
)


def _regex_fallback(message: str) -> tuple[bool, str]:
    if _SUSPICIOUS.search(message):
        return False, "Message may reveal a specific card or pile value"
    return True, ""


async def validate_message(message: str) -> tuple[bool, str]:
    """Return (isAllowed, reason). Fails open (allow) if Claude is unavailable."""
    try:
        response = _client.messages.create(
            model=config.ANTHROPIC_MODEL,
            max_tokens=100,
            system=[{"type": "text", "text": _SYSTEM_PROMPT, "cache_control": {"type": "ephemeral"}}],
            tools=[_VALIDATE_TOOL],
            tool_choice={"type": "any"},
            messages=[{"role": "user", "content": f'Validate this player message: "{message}"'}],
        )
        for block in response.content:
            if block.type == "tool_use" and block.name == "validation_result":
                return block.input["isAllowed"], block.input.get("reason", "")
    except Exception:
        logger.exception("Claude validation error — using regex fallback")
    return _regex_fallback(message)
