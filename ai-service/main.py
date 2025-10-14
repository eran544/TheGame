"""
The Game AI Service
FastAPI microservice for AI player behavior and message validation
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from datetime import datetime
import logging

# Import configuration
from config import config

# Configure logging
logging.basicConfig(
    level=getattr(logging, config.LOG_LEVEL),
    format=config.LOG_FORMAT
)

logger = logging.getLogger(__name__)

# Create FastAPI app
app = FastAPI(
    title="The Game AI Service",
    description="AI microservice for The Game virtual implementation",
    version="1.0.0"
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://localhost:5000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
async def root():
    """Root endpoint"""
    return {"message": "The Game AI Service", "status": "running"}

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.utcnow().isoformat(),
        "service": "ai-service"
    }

# Placeholder endpoints for future implementation
@app.post("/validate-message")
async def validate_message():
    """Validate chat message against game rules"""
    return {"message": "Message validation endpoint - coming soon"}

@app.post("/ai-move")
async def get_ai_move():
    """Get AI player's next move"""
    return {"message": "AI move endpoint - coming soon"}

@app.post("/ai-message")
async def generate_ai_message():
    """Generate AI chat message"""
    return {"message": "AI message generation endpoint - coming soon"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host=config.API_HOST,
        port=config.API_PORT,
        reload=config.API_RELOAD
    )