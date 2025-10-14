# The Game - React Client

React TypeScript client application for The Game virtual implementation.

## Technology Stack

- **React 18** with TypeScript
- **Redux Toolkit** for state management
- **React Router** for navigation
- **SignalR Client** for real-time communication
- **Create React App** for build tooling

## Configuration

### Environment Variables

Copy `.env.template` to `.env` and configure:

```bash
cp .env.template .env
```

Required environment variables:

- `REACT_APP_API_BASE_URL`: Base URL for the server API (default: http://localhost:5000)
- `REACT_APP_SIGNALR_HUB_URL`: SignalR hub URL (default: http://localhost:5000/gamehub)
- `REACT_APP_ENVIRONMENT`: Environment name (development/production)
- `REACT_APP_ENABLE_DEBUG_LOGGING`: Enable debug logging (true/false)

## Development

### Prerequisites

- Node.js 18 or higher
- npm or yarn

### Setup

```bash
# Install dependencies
npm install

# Start development server
npm start
```

The application will be available at http://localhost:3000

### Available Scripts

- `npm start`: Start development server
- `npm build`: Build for production
- `npm test`: Run tests
- `npm run eject`: Eject from Create React App (not recommended)

## Project Structure

```
src/
├── components/          # Reusable UI components
├── pages/              # Page components
├── store/              # Redux store and slices
├── services/           # API and SignalR services
├── types/              # TypeScript type definitions
├── utils/              # Utility functions
└── styles/             # CSS and styling files
```

## Key Features

- **Game Board**: Visual representation of the four card piles
- **Player Hand**: Interactive card selection and playing
- **Real-time Updates**: Live game state synchronization
- **Chat System**: In-game communication with rule enforcement
- **Statistics Dashboard**: Player performance tracking
- **Responsive Design**: Works on desktop and mobile devices

## State Management

The application uses Redux Toolkit with the following slices:

- **authSlice**: User authentication and session management
- **gameSlice**: Game state, board, and player hands
- **chatSlice**: Chat messages and communication
- **uiSlice**: UI state and loading indicators

## API Integration

The client communicates with the server through:

- **REST API**: Standard HTTP requests for game actions
- **SignalR**: Real-time updates and multiplayer communication

## Testing

```bash
# Run tests
npm test

# Run tests with coverage
npm test -- --coverage
```

## Building for Production

```bash
# Create production build
npm run build
```

The build artifacts will be stored in the `build/` directory.