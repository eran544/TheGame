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
from game_models import AIMoveRequest, AIMoveResponse
from ai_player import get_ai_move
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


@app.post("/ai-move", response_model=AIMoveResponse)
async def ai_move(request: AIMoveRequest) -> AIMoveResponse:
    """Return an AI player's move for the given game state."""
    return await get_ai_move(request)


class ValidateMessageRequest(BaseModel):
    message: str

class ValidateMessageResponse(BaseModel):
    isAllowed: bool
    reason: str

@app.post("/validate-message", response_model=ValidateMessageResponse)
async def validate_message(request: ValidateMessageRequest) -> ValidateMessageResponse:
    """Validate a chat message against The Game communication rules."""
    is_allowed, reason = await _validate_chat(request.message)
    return ValidateMessageResponse(isAllowed=is_allowed, reason=reason)


@app.post("/ai-message")
async def generate_ai_message():
    """Generate AI chat message — coming in Task 15."""
    return {"message": "AI message generation endpoint — coming soon"}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "main:app",
        host=config.API_HOST,
        port=config.API_PORT,
        reload=config.API_RELOAD,
    )
