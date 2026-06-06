# The Game - .NET Server

ASP.NET Core Web API server with SignalR for The Game virtual implementation.

## Technology Stack

- **ASP.NET Core 8** Web API
- **Entity Framework Core** with SQL Server
- **SignalR** for real-time communication
- **JWT Authentication** for security
- **Redis** for caching and session management
- **BCrypt** for password hashing

## Configuration

### Application Settings

Copy `appsettings.template.json` to `appsettings.json` and configure:

```bash
cp appsettings.template.json appsettings.json
```

Required configuration sections:

#### Connection Strings
- `DefaultConnection`: SQL Server connection string
- `Redis`: Redis connection string

#### JWT Settings
- `SecretKey`: JWT signing key (minimum 32 characters)
- `Issuer`: JWT issuer (default: TheGameServer)
- `Audience`: JWT audience (default: TheGameClient)
- `ExpirationMinutes`: Token expiration time

#### AI Service
- `BaseUrl`: URL for the Python AI microservice
- `TimeoutSeconds`: Request timeout for AI service calls

#### Game Settings
- `SessionTimeoutMinutes`: User session timeout
- `GameTimeoutMinutes`: Game session timeout
- `MaxPlayersPerGame`: Maximum players per game session

## Development

### Prerequisites

- .NET 8 SDK
- SQL Server (or Docker container)
- Redis (or Docker container)

### Setup

```bash
# Restore packages
dotnet restore

# Update database
dotnet ef database update

# Run the application
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

### Database Migrations

```bash
# Add new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Remove last migration (if not applied)
dotnet ef migrations remove
```

## Project Structure

```
├── Controllers/         # API controllers
├── Hubs/               # SignalR hubs
├── Models/             # Data models and DTOs
├── Services/           # Business logic services
├── Data/               # Entity Framework context and configurations
├── Middleware/         # Custom middleware
└── Extensions/         # Service registration extensions
```

## Key Features

- **Authentication**: JWT-based user authentication
- **Game Management**: Game session creation and management
- **Real-time Communication**: SignalR hubs for live updates
- **Statistics Tracking**: Player performance and game history
- **Admin Dashboard**: Administrative functions and monitoring
- **AI Integration**: Communication with Python AI microservice

## API Endpoints

### Authentication
- `POST /api/auth/register`: User registration
- `POST /api/auth/login`: User login
- `POST /api/auth/logout`: User logout
- `GET /api/auth/profile`: Get user profile

### Game Management
- `POST /api/games`: Create new game
- `GET /api/games/{id}`: Get game details
- `POST /api/games/{id}/join`: Join game
- `POST /api/games/{id}/leave`: Leave game
- `POST /api/games/{id}/play`: Play cards

### Statistics
- `GET /api/stats/player`: Get player statistics
- `GET /api/stats/admin`: Get admin statistics (admin only)

## SignalR Hubs

### GameHub
- `JoinGameGroup`: Join game-specific group
- `LeaveGameGroup`: Leave game group
- `SendChatMessage`: Send chat message

### Hub Events (Server to Client)
- `GameUpdated`: Game state changed
- `PlayerJoined`: Player joined game
- `PlayerLeft`: Player left game
- `ChatMessage`: New chat message
- `GameEnded`: Game finished

## Security

- **JWT Authentication**: Secure token-based authentication
- **Password Hashing**: BCrypt for secure password storage
- **Input Validation**: Comprehensive request validation
- **CORS**: Configured for client application
- **Rate Limiting**: API rate limiting (to be implemented)

## Testing

```bash
# Run unit tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Docker Support

```bash
# Build Docker image
docker build -t thegame-server .

# Run container
docker run -p 5000:8080 thegame-server
```