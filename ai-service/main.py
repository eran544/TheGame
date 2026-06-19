"""
The Game AI Service
FastAPI microservice for AI player behaviour and message validation
"""

import logging
from datetime import datetime

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from config import config
from pydantic import BaseModel
from game_models import (
    AIMoveRequest,
    AIMoveResponse,
    AIMessageRequest,
    AIMessageResponse,
    Flip7AIMoveRequest,
    Flip7AIMoveResponse,
)
from ai_player import get_ai_move, get_ai_message
from flip7_ai import get_flip7_move
from message_validator import validate_message as _validate_chat

logging.basicConfig(
    level=getattr(logging, config.LOG_LEVEL),
    format=config.LOG_FORMAT,
)

logger = logging.getLogger(__name__)

app = FastAPI(
    title="The Game AI Service",
    description="AI microservice for The Game virtual implementation",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://localhost:5000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/")
async def root():
    return {"message": "The Game AI Service", "status": "running"}


@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "timestamp": datetime.utcnow().isoformat(),
        "service": "ai-service",
    }


class ValidateMessageRequest(BaseModel):
    message: str

class ValidateMessageResponse(BaseModel):
    isAllowed: bool
    reason: str


# ── The Game endpoints ────────────────────────────────────────────────────────
# Namespaced under /the-game so a second game (Flip 7) can add its own /flip7/*
# endpoints alongside. The legacy unprefixed routes are kept as aliases for
# backward compatibility with existing callers.

@app.post("/the-game/ai-move", response_model=AIMoveResponse)
@app.post("/ai-move", response_model=AIMoveResponse)
async def ai_move(request: AIMoveRequest) -> AIMoveResponse:
    """Return an AI player's move for the given game state."""
    return await get_ai_move(request)


@app.post("/the-game/validate-message", response_model=ValidateMessageResponse)
@app.post("/validate-message", response_model=ValidateMessageResponse)
async def validate_message(request: ValidateMessageRequest) -> ValidateMessageResponse:
    """Validate a chat message against The Game communication rules."""
    is_allowed, reason = await _validate_chat(request.message)
    return ValidateMessageResponse(isAllowed=is_allowed, reason=reason)


@app.post("/the-game/ai-message", response_model=AIMessageResponse)
@app.post("/ai-message", response_model=AIMessageResponse)
async def generate_ai_message(request: AIMessageRequest) -> AIMessageResponse:
    """Generate a cooperative chat message for an AI player."""
    return await get_ai_message(request)


# ── Flip 7 endpoints ──────────────────────────────────────────────────────────
# Press-your-luck Hit/Stay decisions, reusing the shared difficulty/style knobs.

@app.post("/flip7/ai-move", response_model=Flip7AIMoveResponse)
async def flip7_ai_move(request: Flip7AIMoveRequest) -> Flip7AIMoveResponse:
    """Return an AI player's Hit/Stay decision for the given Flip 7 state."""
    return await get_flip7_move(request)


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "main:app",
        host=config.API_HOST,
        port=config.API_PORT,
        reload=config.API_RELOAD,
    )
