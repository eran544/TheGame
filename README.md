# The Game - Virtual Implementation

A virtual implementation of "The Game" - a cooperative card game where players work together to play all 98 numbered cards (2-99) onto four piles.

## About The Game

"The Game" is a cooperative card game created by Steffen Benndorf and published by Nürnberger-Spielkarten-Verlag. This virtual implementation was created for educational purposes to demonstrate modern web development techniques and distributed system architecture.

### Game Rules

The Game is a cooperative card game where all players work together to play cards from their hands onto four shared piles:

- **Two Ascending Piles**: Start at 1, cards must be played in ascending order
- **Two Descending Piles**: Start at 100, cards must be played in descending order
- **The Backwards Trick**: You can play a card that is exactly 10 less on an ascending pile, or exactly 10 more on a descending pile

**Objective**: Play all 98 cards (numbered 2-99) to win the game.

**Turn Requirements**:
- When the draw pile exists: Play at least 2 cards per turn
- When the draw pile is empty: Play at least 1 card per turn
- After playing cards, draw back up to your starting hand size (if cards remain in draw pile)

**Communication Rules**: 
- Players can discuss strategy but cannot reveal specific card numbers
- Allowed: "I can play on the ascending pile", "I have something good for that pile"
- Not allowed: "I have a 47", "Play your 23 next"

## Project Structure

This implementation consists of three main components:

- **Client** (`/client`): React TypeScript application with Redux Toolkit
- **Server** (`/server`): ASP.NET Core Web API with SignalR for real-time communication
- **AI Service** (`/ai-service`): Python FastAPI microservice for AI players and message validation

## Development Setup

### Prerequisites

- Docker and Docker Compose
- Node.js 18+ (for local client development)
- .NET 8 SDK (for local server development)
- Python 3.11+ (for local AI service development)

### Quick Start with Docker

1. Clone the repository
2. Copy configuration templates:
   ```bash
   cp client/.env.template client/.env
   cp server/appsettings.template.json server/appsettings.json
   cp ai-service/config.template.py ai-service/config.py
   ```
3. Update configuration files with your settings (see Configuration section below)
4. Start all services:
   ```bash
   docker-compose up -d
   ```

The application will be available at:
- Client: http://localhost:3000
- Server API: http://localhost:5000
- AI Service: http://localhost:8000
- SQL Server: localhost:1433
- Redis: localhost:6379

### Configuration

#### Client Configuration (`.env`)
- `REACT_APP_API_BASE_URL`: Base URL for the server API
- `REACT_APP_SIGNALR_HUB_URL`: URL for SignalR hub connection

#### Server Configuration (`appsettings.json`)
- `ConnectionStrings.DefaultConnection`: SQL Server connection string
- `ConnectionStrings.Redis`: Redis connection string
- `JwtSettings.SecretKey`: JWT signing key (minimum 32 characters)
- `AIService.BaseUrl`: URL for the AI microservice

#### AI Service Configuration (`config.py`)
- `OPENAI_API_KEY`: Your OpenAI API key for AI functionality
- `REDIS_HOST` and `REDIS_PORT`: Redis connection details

### Local Development

Each service can be run locally for development:

#### Client
```bash
cd client
npm install
npm start
```

#### Server
```bash
cd server
dotnet restore
dotnet run
```

#### AI Service
```bash
cd ai-service
pip install -r requirements.txt
uvicorn main:app --reload
```

## Implementation Phases

The project is implemented in four phases:

1. **Phase 1**: Single-player implementation with basic game mechanics
2. **Phase 2**: Multiplayer online functionality with real-time communication
3. **Phase 3**: AI players and disconnection handling
4. **Phase 4**: Expert rules, spectator system, and advanced features

## Credits

- Original game "The Game" created by Steffen Benndorf
- Published by Nürnberger-Spielkarten-Verlag
- This virtual implementation created for educational purposes

## License

This project is created for educational purposes. The original game "The Game" is copyrighted by its respective creators and publishers.