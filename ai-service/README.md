# The Game - AI Service

Python FastAPI microservice providing AI player behavior and message validation for The Game virtual implementation.

## Technology Stack

- **FastAPI** for high-performance API
- **OpenAI API** for AI decision making and message validation
- **Redis** for caching and state management
- **Pydantic** for data validation
- **Uvicorn** as ASGI server

## Configuration

### Configuration File

Copy `config.template.py` to `config.py` and configure:

```bash
cp config.template.py config.py
```

Required configuration:

#### OpenAI Settings
- `OPENAI_API_KEY`: Your OpenAI API key
- `OPENAI_MODEL`: Model to use (default: gpt-3.5-turbo)
- `OPENAI_MAX_TOKENS`: Maximum tokens per response
- `OPENAI_TEMPERATURE`: Response creativity (0.0-1.0)

#### Redis Settings
- `REDIS_HOST`: Redis server host
- `REDIS_PORT`: Redis server port
- `REDIS_DB`: Redis database number
- `REDIS_PASSWORD`: Redis password (if required)

#### API Settings
- `API_HOST`: Host to bind to (default: 0.0.0.0)
- `API_PORT`: Port to listen on (default: 8000)
- `API_RELOAD`: Enable auto-reload for development

#### Game Settings
- `MAX_MESSAGE_LENGTH`: Maximum chat message length
- `AI_DECISION_TIMEOUT`: Timeout for AI decisions (seconds)
- `MESSAGE_VALIDATION_TIMEOUT`: Timeout for message validation (seconds)

## Development

### Prerequisites

- Python 3.11 or higher
- OpenAI API key
- Redis server

### Setup

```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run the service
uvicorn main:app --reload
```

The API will be available at:
- HTTP: http://localhost:8000
- API Documentation: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc

## Project Structure

```
├── main.py             # FastAPI application entry point
├── config.py           # Configuration settings
├── models/             # Pydantic models and data structures
├── services/           # Business logic services
│   ├── ai_player.py    # AI player decision making
│   ├── message_validator.py  # Chat message validation
│   └── game_context.py # Game context management
├── utils/              # Utility functions
└── tests/              # Unit tests
```

## Key Features

- **Message Validation**: AI-powered chat message rule enforcement
- **AI Player Decisions**: Intelligent card playing for AI players
- **Game Context Awareness**: Understanding of game state for better decisions
- **Caching**: Redis-based caching for improved performance
- **Error Handling**: Robust error handling with fallback mechanisms

## API Endpoints

### Message Validation
- `POST /validate-message`: Validate chat message against game rules

### AI Player Actions
- `POST /ai-move`: Get AI player's next move
- `POST /ai-message`: Generate AI chat message

### Health Check
- `GET /health`: Service health status

## Request/Response Models

### Message Validation
```python
class MessageValidationRequest(BaseModel):
    message: str
    game_context: GameContext
    player_id: str

class ValidationResult(BaseModel):
    is_valid: bool
    reason: Optional[str]
    suggested_alternative: Optional[str]
```

### AI Move Request
```python
class AIMoveRequest(BaseModel):
    game_state: GameState
    player_hand: List[int]
    player_id: str

class AIMoveResponse(BaseModel):
    moves: List[CardPlay]
    reasoning: Optional[str]
```

## AI Behavior

### Message Validation Rules
The AI validates messages against The Game's communication rules:
- ✅ Allowed: General strategy discussion, pile preferences
- ❌ Forbidden: Specific card numbers, exact hand contents
- ✅ Allowed: "I can help with the ascending pile"
- ❌ Forbidden: "I have a 47" or "Play your 23"

### AI Player Strategy
The AI player considers:
- Current pile states and valid moves
- Hand optimization and card conservation
- Cooperative gameplay principles
- Risk assessment for different plays
- Backwards trick opportunities

## Error Handling

- **OpenAI API Failures**: Automatic retry with exponential backoff
- **Timeout Handling**: Configurable timeouts with fallback responses
- **Invalid Requests**: Comprehensive input validation
- **Service Unavailable**: Graceful degradation with rule-based fallbacks

## Testing

```bash
# Run unit tests
pytest

# Run tests with coverage
pytest --cov=.

# Run specific test file
pytest tests/test_message_validator.py
```

## Docker Support

```bash
# Build Docker image
docker build -t thegame-ai-service .

# Run container
docker run -p 8000:8000 thegame-ai-service
```

## Performance Considerations

- **Caching**: Frequently used AI decisions cached in Redis
- **Request Batching**: Multiple requests batched when possible
- **Model Selection**: Appropriate OpenAI model for each task type
- **Timeout Management**: Reasonable timeouts to prevent blocking

## Monitoring

The service provides:
- Health check endpoint for monitoring
- Structured logging for debugging
- Performance metrics (to be implemented)
- Error tracking and alerting (to be implemented)