"""
Configuration template for The Game AI Service
Copy this file to config.py and fill in the actual values
"""

from typing import Optional

class Config:
    # Anthropic Configuration
    ANTHROPIC_API_KEY: str = "YOUR_ANTHROPIC_API_KEY_HERE"
    # claude-haiku-4-5-20251001 is recommended for low-latency validation calls;
    # switch to claude-sonnet-4-6 for higher-quality AI player decisions.
    ANTHROPIC_MODEL: str = "claude-haiku-4-5-20251001"
    ANTHROPIC_MAX_TOKENS: int = 150

    # Redis Configuration
    REDIS_HOST: str = "localhost"
    REDIS_PORT: int = 6379
    REDIS_DB: int = 0
    REDIS_PASSWORD: Optional[str] = None

    # API Configuration
    API_HOST: str = "0.0.0.0"
    API_PORT: int = 8000
    API_RELOAD: bool = True

    # Game Configuration
    MAX_MESSAGE_LENGTH: int = 500
    AI_DECISION_TIMEOUT: int = 10
    MESSAGE_VALIDATION_TIMEOUT: int = 5

    # Logging Configuration
    LOG_LEVEL: str = "INFO"
    LOG_FORMAT: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"

# Create config instance
config = Config()
