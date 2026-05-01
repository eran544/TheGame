# Implementation Plan

## Phase 1: Single Player Implementation

- [x] 1. Set up project structure and development environment





  - Create React client project with TypeScript and Redux Toolkit
  - Set up ASP.NET Core Web API project with SignalR
  - Create Python FastAPI microservice project
  - Configure Docker Compose for local development
  - Set up SQL Server and Redis containers
  - _Requirements: 5.1, 5.2_

- [x] 1.1 Create configuration management system


  - Create appsettings.template.json for .NET server with placeholder values
  - Create appsettings.json for .NET server with actual connection strings and API keys
  - Create .env.template file for React client with placeholder API endpoints
  - Create .env file for React client with actual API endpoints
  - Create config.template.py for Python AI service with placeholder OpenAI API key
  - Create config.py for Python AI service with actual OpenAI API key and settings
  - Create .gitignore file and add appsettings.json, .env, and config.py to it
  - Document configuration setup in README files for each project.
  - Add readme.md for the game itself, describing the rules, crediting the creators of the game and stating it was created for educational purposes
  - _Requirements: 5.1, 5.2_

- [x] 2. Implement core data models and database schema
  - Create Entity Framework models for Users, GameSessions, GameStates, and Statistics
  - Implement database migrations for all tables
  - Set up Redis connection and caching models
  - Create TypeScript interfaces for client-side data models
  - _Requirements: 5.3, 1.0, 1.6_

- [x] 3. Build authentication and user management system
  - Implement user registration with password validation
  - Create login/logout functionality with JWT tokens
  - Build session management with Redis caching
  - Implement automatic logout for inactivity timeouts
  - Create super admin account initialization
  - _Requirements: 1.0_

- [x]* 3.1 Write unit tests for authentication services
  - Test user registration validation
  - Test login/logout flows
  - Test session timeout handling
  - _Requirements: 1.0_

- [x] 4. Create basic game engine and card logic
  - Implement card deck initialization and shuffling
  - Create game state management for single-player games
  - Build card playing validation (ascending/descending rules)
  - Implement backwards trick logic (1±10 rule)
  - Create game end detection and scoring
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x]* 4.1 Write unit tests for game logic
  - Test card validation rules
  - Test backwards trick implementation
  - Test game end conditions
  - Test scoring calculations
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 5. Build React client foundation and Redux store
  - Set up Redux store with game state slices
  - Create authentication slice and middleware
  - Implement routing with protected routes
  - Build basic UI components (buttons, cards, layouts)
  - Create responsive design system
  - _Requirements: 5.1, 1.0_

- [x] 6. Implement single-player game UI components
  - Create GameController with endpoints: start game, play turn, get game state, abandon game
  - Add game Redux slice actions: startGame, playTurn, fetchGameState, abandonGame with async thunks
  - Create GameBoard component with four piles display (ascending/descending labels and top card values)
  - Build PlayerHand component with card selection (click to select/deselect)
  - Implement click-to-play mechanics: select card then select pile to submit the turn
  - Create game status display (draw pile count, turn minimum cards indicator)
  - Build game end modal with score, result label, and return to menu button
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 6.1 Fix single-player card play behaviour
  - Change single-player mode to immediate play: selecting a card then a pile sends the play instantly, no staging or "Play Turn" button
  - Server: set minCardsPerTurn to 1 for single-player games (no turn minimum — player plays continuously)
  - Client: compute valid piles for the selected card and highlight only those; grey out invalid piles
  - Client: fix API error message parsing — server error responses use an `error` field, not `message` or `title`
  - _Requirements: 1.2, 1.3, 1.4_

- [x] 7. Create player statistics and dashboard system
  - Implement statistics tracking service in .NET
  - Build database operations for game result storage
  - Create player dashboard UI with statistics display
  - Implement game history viewing functionality
  - Add statistics calculation and aggregation
  - _Requirements: 1.6_

- [x]* 7.1 Write unit tests for statistics service
  - Test game result recording
  - Test statistics calculations
  - Test dashboard data retrieval
  - _Requirements: 1.6_

- [ ] 8. Integrate real-time communication for Phase 1
  - Implement SignalR hub for real-time game state updates
  - Add loading states and optimistic UI updates in the React client
  - Implement client-side and server-side input validation
  - Add error boundaries and user-facing error messages
  - _Requirements: 5.1, 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 9. Build interactive game instructions page
  - Add "How to Play" button on the main menu that navigates to the instructions page
  - Create a multi-section instructions layout with next/previous navigation
  - Implement animated visual examples for ascending and descending pile rules
  - Build an interactive backwards trick demo (clickable card on a pile showing the ±10 move)
  - Add a "Back to Menu" button on the instructions page
  - _Requirements: 1.7_

---

## Phase 2: Multiplayer Online Implementation

- [ ] 10. Build multiplayer game session management
  - Create game lobby system for creating/joining games
  - Implement multiplayer game state synchronization
  - Build turn-based gameplay mechanics
  - Add player list display and status indicators
  - Implement game session cleanup and management
  - _Requirements: 2.1, 2.3_

- [ ] 11. Implement real-time multiplayer communication
  - Extend SignalR hub for multiplayer broadcasts
  - Create real-time game state updates
  - Implement player action broadcasting
  - Add disconnection detection and game termination for Phase 2
  - Build reconnection handling for temporary disconnects
  - _Requirements: 2.2_

- [ ]* 11.1 Write integration tests for multiplayer functionality
  - Test game session creation and joining
  - Test real-time state synchronization
  - Test disconnection handling
  - _Requirements: 2.1, 2.2, 2.3_

- [ ] 12. Create Python AI microservice foundation
  - Set up FastAPI project with OpenAI integration
  - Create API endpoints for message validation and AI moves
  - Implement basic message validation against communication rules
  - Build AI player decision-making logic
  - Add error handling and fallback mechanisms
  - _Requirements: 5.2, 2.4_

- [ ] 13. Build chat system with rule enforcement
  - Create chat UI components in React client
  - Implement chat message API endpoints
  - Integrate AI message validation service
  - Add real-time chat broadcasting via SignalR
  - Create message blocking and warning system
  - _Requirements: 2.4_

- [ ]* 13.1 Write unit tests for chat validation
  - Test forbidden message detection
  - Test context-aware validation
  - Test message broadcasting
  - _Requirements: 2.4_

- [ ] 14. Implement admin dashboard and management features
  - Create admin-only UI components and routes
  - Build admin statistics dashboard
  - Implement user management functionality (create/delete/reset passwords)
  - Add game monitoring and intervention capabilities
  - Create AI performance tracking and display
  - _Requirements: 2.5_

---

## Phase 3: AI Players and Disconnection Handling

- [ ] 15. Build AI player integration system
  - Extend AI microservice for full AI player behavior
  - Implement AI player hand management
  - Create AI decision-making for card plays
  - Add AI chat message generation
  - Integrate AI players into multiplayer games
  - _Requirements: 3.1, 3.3_

- [ ] 16. Implement disconnection handling and AI replacement
  - Create disconnection detection with timeout handling
  - Build AI replacement system for disconnected players
  - Implement hand and context transfer to AI
  - Add reconnection and position reclaim functionality
  - Update statistics handling for AI-assisted games
  - _Requirements: 3.2_

- [ ]* 16.1 Write integration tests for AI replacement
  - Test disconnection detection
  - Test AI replacement process
  - Test reconnection handling
  - Test statistics recording for AI-assisted games
  - _Requirements: 3.2_

---

## Phase 4: Expert Rules and Spectator System

- [ ] 17. Create expert rules and rule customization system
  - Implement expert variant with increased card requirements
  - Build rule customization UI and validation
  - Create custom rule storage and application
  - Update AI behavior for custom rules
  - Add rule display and communication to players
  - _Requirements: 4.1, 4.2_

- [ ] 18. Build spectator system
  - Create spectator join functionality
  - Implement spectator-specific game view
  - Build spectator list display for players
  - Add spectator communication restrictions
  - Create spectator UI with limited game information
  - _Requirements: 4.3_

- [ ] 19. Implement admin spectator privileges
  - Create admin spectator mode with enhanced viewing
  - Implement invisible admin spectating
  - Add admin ability to view any player's hand
  - Ensure admin spectators don't appear in player lists
  - Maintain communication restrictions for admin spectators
  - _Requirements: 4.4_

- [ ]* 19.1 Write end-to-end tests for spectator functionality
  - Test spectator joining and viewing
  - Test admin spectator privileges
  - Test spectator restrictions and limitations
  - _Requirements: 4.3, 4.4_

---

## Cross-Phase: Infrastructure and Deployment

- [ ] 20. Implement comprehensive error handling and resilience
  - Add circuit breaker patterns for external service calls
  - Implement retry logic with exponential backoff
  - Create graceful degradation for AI service failures
  - Add comprehensive logging and monitoring
  - Build health check endpoints for all services
  - _Requirements: 5.1, 5.2_

- [ ] 21. Performance optimization and caching
  - Implement Redis caching for frequently accessed data
  - Add database query optimization and indexing
  - Create efficient SignalR message batching
  - Implement client-side caching and memoization
  - Add performance monitoring and metrics collection
  - _Requirements: 5.1, 5.3_

- [ ]* 21.1 Write performance tests
  - Test concurrent game sessions
  - Test AI service response times
  - Test database performance under load
  - Test real-time communication scalability
  - _Requirements: 5.1, 5.2, 5.3_

- [ ] 22. Security implementation and hardening
  - Implement comprehensive input validation and sanitization
  - Add rate limiting for API endpoints
  - Create secure session management
  - Implement CSRF protection
  - Add security headers and HTTPS enforcement
  - _Requirements: 5.1, 1.0, 2.5_

- [ ] 23. Final integration and deployment preparation
  - Create Docker containers for all services
  - Set up production configuration management
  - Implement database migration scripts
  - Create deployment documentation
  - Build CI/CD pipeline configuration
  - _Requirements: 5.1, 5.2, 5.3_

- [ ]* 23.1 Write comprehensive integration tests
  - Test full game flows across all phases
  - Test cross-service communication
  - Test deployment and configuration
  - Test backup and recovery procedures
  - _Requirements: 5.1, 5.2, 5.3_