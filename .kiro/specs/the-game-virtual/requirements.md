# Requirements Document

## Introduction

This document outlines the requirements for implementing a virtual version of "The Game" - a cooperative card game where players work together to play all 98 numbered cards (2-99) onto four piles. The implementation will be developed in four distinct phases, starting with a single-player version and progressively adding multiplayer capabilities, AI players, and advanced rule modifications.

The system architecture consists of three main components:
- React client for game visualization and user interaction
- .NET server for game logic, state management, and API services
- Python AI microservice for message validation and AI player behavior

## Requirements

### Phase 1: Single Player Implementation

#### Requirement 1.0: User Authentication and Registration

**User Story:** As a new user, I want to register and login so that I can access the game and track my progress.

##### Acceptance Criteria

1. WHEN a new user visits the application THEN the system SHALL provide a registration form requiring username, password, and password confirmation
2. WHEN a user registers THEN the system SHALL validate password requirements (minimum 8 characters, at least one uppercase, one lowercase, one number)
3. WHEN a user attempts to register with an existing username THEN the system SHALL prevent registration and display an appropriate error message
4. WHEN a user logs in THEN the system SHALL validate credentials and create a session
5. WHEN a user is inactive in the lobby for 10 minutes THEN the system SHALL automatically log them out
6. WHEN a user is inactive during a game for 1.5 minutes THEN the system SHALL disconnect them and replace with AI or end the game before implementing phase 3.
7. WHEN the system initializes THEN the system SHALL have a pre-created super admin account for administrative functions

#### Requirement 1.1: Game Setup and Initialization

**User Story:** As a player, I want to start a new single-player game so that I can play The Game virtually.

##### Acceptance Criteria

1. WHEN the player starts a new game THEN the system SHALL initialize four row piles (2 ascending starting at 1, 2 descending starting at 100)
2. WHEN the game initializes THEN the system SHALL create a shuffled deck of 98 numbered cards (2-99)
3. WHEN the game starts THEN the system SHALL deal 8 cards to the player's hand
4. WHEN the game setup is complete THEN the system SHALL display the game board with four piles and the player's hand
5. WHEN the game initializes THEN the system SHALL create a draw pile with the remaining cards

#### Requirement 1.2: Card Playing Mechanics

**User Story:** As a player, I want to play cards according to the game rules so that I can progress through the game.

##### Acceptance Criteria

1. WHEN playing on an ascending pile THEN the system SHALL only allow cards with values greater than the current top card
2. WHEN playing on a descending pile THEN the system SHALL only allow cards with values smaller than the current top card
3. WHEN the player attempts an invalid move THEN the system SHALL prevent the action and display an appropriate error message
4. WHEN the player plays cards THEN the system SHALL update the pile visually showing only the top card
5. WHEN the player completes their turn THEN the system SHALL draw cards from the draw pile to refill their hand to the original count
6. WHEN the draw pile is empty THEN the system SHALL not attempt to refill the player's hand

#### Requirement 1.3: Backwards Trick Implementation

**User Story:** As a player, I want to use the backwards trick so that I can create strategic opportunities to play more cards.

##### Acceptance Criteria

1. WHEN playing on an ascending pile THEN the system SHALL allow a card that is exactly 10 less than the top card
2. WHEN playing on a descending pile THEN the system SHALL allow a card that is exactly 10 greater than the top card
3. WHEN the backwards trick is used THEN the system SHALL visually indicate this special move
4. WHEN the backwards trick is available THEN the system SHALL highlight valid backwards trick moves in the UI

#### Requirement 1.4: Card Play Flow

**User Story:** As a player, I want clear card play mechanics so that I understand how to play cards and progress through the game.

##### Acceptance Criteria

1. WHEN a player selects a card in single-player mode THEN the system SHALL highlight only the piles where that card can legally be played
2. WHEN a player selects a card and a valid pile in single-player mode THEN the system SHALL immediately apply the play without requiring a separate "submit turn" action
3. WHEN the player plays a card THEN the system SHALL immediately draw a replacement card from the draw pile if cards are available
4. WHEN the player has no valid move on any pile THEN the system SHALL end the game
5. WHEN it is a player's turn in multiplayer mode and the draw pile is not empty THEN the system SHALL require playing at least 2 cards before ending the turn
6. WHEN it is a player's turn in multiplayer mode and the draw pile is empty THEN the system SHALL require playing at least 1 card before ending the turn
7. WHEN the player cannot play the minimum required cards in multiplayer THEN the system SHALL end the game for all players
8. WHEN the player attempts an invalid move THEN the system SHALL prevent the play and display the specific reason

#### Requirement 1.5: Game End and Scoring

**User Story:** As a player, I want to see my game results so that I can track my performance and improvement.

##### Acceptance Criteria

1. WHEN the player cannot play the minimum required cards THEN the system SHALL end the game
2. WHEN all 98 cards are played THEN the system SHALL declare a perfect game victory
3. WHEN the game ends THEN the system SHALL calculate the score based on remaining cards in hand and draw pile
4. WHEN the game ends THEN the system SHALL display the final score with performance rating (Perfect/Excellent/Try Again)
5. WHEN the game ends THEN the system SHALL save the game result to the player's statistics
6. WHEN the game ends THEN the system SHALL offer the option to start a new game

#### Requirement 1.6: Player Statistics and Dashboard

**User Story:** As a player, I want to view my game statistics so that I can track my progress and improvement over time.

##### Acceptance Criteria

1. WHEN a player accesses their dashboard THEN the system SHALL display total games played
2. WHEN a player views statistics THEN the system SHALL show number of perfect games achieved
3. WHEN a player checks their performance THEN the system SHALL display average remaining cards across all games
4. WHEN a player views their dashboard THEN the system SHALL show their best score (fewest remaining cards)
5. WHEN a player accesses statistics THEN the system SHALL maintain complete game history for the player
6. WHEN displaying game information THEN the system SHALL show how many cards remain in the deck during active games

#### Requirement 1.7: Interactive Game Instructions

**User Story:** As a new player, I want to access interactive game instructions from the main menu so that I can learn how to play before starting a game.

##### Acceptance Criteria

1. WHEN a player is on the main menu THEN the system SHALL display a button linking to the game instructions
2. WHEN a player opens the instructions THEN the system SHALL present the rules in an interactive, step-by-step format
3. WHEN a player views the instructions THEN the system SHALL include animated or visual examples of ascending and descending pile rules
4. WHEN a player views the instructions THEN the system SHALL demonstrate the backwards trick with an interactive example
5. WHEN a player views the instructions THEN the system SHALL allow navigating between rule sections (e.g. next/previous)
6. WHEN a player finishes reading the instructions THEN the system SHALL provide a button to return to the main menu

#### Requirement 1.8: Undo Last Move

**User Story:** As a single-player, I want to undo my last card play so that I can correct mistakes and reconsider my strategy.

##### Acceptance Criteria

1. WHEN playing in single-player mode THEN the system SHALL provide an undo action to revert the most recently played card
2. WHEN the player triggers undo THEN the system SHALL return the last played card to the player's hand
3. WHEN the player triggers undo THEN the system SHALL restore the affected pile to its value before that card was played
4. WHEN the player triggers undo AND a replacement card was drawn after the play THEN the system SHALL return that replacement card to the top of the draw pile
5. WHEN no card has been played yet in the game OR immediately after undo has been used THEN the system SHALL disable the undo action
6. WHEN playing in multiplayer mode THEN the system SHALL NOT provide the undo option

### Phase 2: Multiplayer Online Implementation

#### Requirement 2.1: Multiplayer Game Sessions

**User Story:** As a player, I want to create and join multiplayer games so that I can play cooperatively with other players online.

##### Acceptance Criteria

1. WHEN a player creates a game THEN the system SHALL generate a unique game session ID
2. WHEN a player joins a game THEN the system SHALL validate the session exists and has available slots
3. WHEN players join a game THEN the system SHALL support 2-5 players per session
4. WHEN the game starts THEN the system SHALL deal cards according to player count (2 players: 7 cards each, 3-5 players: 6 cards each)
5. WHEN a player joins or leaves THEN the system SHALL notify all other players in the session

#### Requirement 2.2: Real-time Communication

**User Story:** As a player, I want real-time updates during multiplayer games so that I can see other players' actions immediately.

##### Acceptance Criteria

1. WHEN a player makes a move THEN the system SHALL broadcast the update to all players in real-time
2. WHEN it's a player's turn THEN the system SHALL notify all players whose turn it is
3. WHEN the game state changes THEN the system SHALL synchronize the state across all connected clients
4. WHEN a player disconnects in Phase 2 THEN the system SHALL end the game immediately without saving statistics
5. WHEN a player reconnects THEN the system SHALL restore their game state and hand

#### Requirement 2.3: Turn-Based Multiplayer Logic

**User Story:** As a player, I want structured turn-based gameplay so that multiplayer games proceed in an orderly fashion.

##### Acceptance Criteria

1. WHEN the game starts THEN the system SHALL establish a turn order among players
2. WHEN it's a player's turn THEN the system SHALL only allow that player to make moves
3. WHEN a player completes their turn THEN the system SHALL advance to the next player
4. WHEN a player's turn times out THEN the system SHALL implement appropriate timeout handling
5. WHEN the active player cannot make valid moves THEN the system SHALL end the game for all players

#### Requirement 2.4: Chat System with Rule Enforcement

**User Story:** As a player, I want to communicate with other players while following the game's communication rules so that we can coordinate strategy legally.

##### Acceptance Criteria

1. WHEN a player sends a message THEN the system SHALL validate the message against communication rules before broadcasting
2. WHEN a message contains forbidden information (specific card numbers) THEN the system SHALL block the message and notify the sender
3. WHEN a message is allowed THEN the system SHALL broadcast it to all players in the session
4. WHEN validating messages THEN the system SHALL consider game context to provide enhanced rule enforcement
5. WHEN a player sends multiple rule-violating messages THEN the system SHALL implement appropriate warnings or restrictions

#### Requirement 2.5: Admin Dashboard and Management

**User Story:** As an admin, I want access to administrative functions so that I can manage the system and monitor game activity.

##### Acceptance Criteria

1. WHEN an admin logs in THEN the system SHALL provide access to the admin dashboard
2. WHEN an admin views statistics THEN the system SHALL display total games, active players, and server performance metrics
3. WHEN an admin manages users THEN the system SHALL allow creating, deleting, and resetting user passwords
4. WHEN an admin monitors games THEN the system SHALL provide information about disconnections and AI performance
5. WHEN an admin needs to intervene THEN the system SHALL allow force-ending games and kicking players
6. WHEN tracking AI performance THEN the system SHALL record decision time, win rate when replacing players, and message validation accuracy

### Phase 3: AI Players and Disconnection Handling

#### Requirement 3.1: AI Player Integration

**User Story:** As a player, I want to add AI players to games so that I can play with fewer human players or practice against AI opponents.

##### Acceptance Criteria

1. WHEN creating a game THEN the system SHALL allow adding AI players to fill empty slots
2. WHEN it's an AI player's turn THEN the system SHALL request moves from the AI microservice
3. WHEN an AI player makes decisions THEN the system SHALL apply the same rule validation as human players
4. WHEN an AI player communicates THEN the system SHALL ensure messages follow the same communication rules
5. WHEN an AI player joins THEN the system SHALL treat it equivalently to human players in game mechanics

#### Requirement 3.2: Disconnection and AI Replacement

**User Story:** As a player, I want AI players to replace disconnected players so that games can continue when someone leaves unexpectedly.

##### Acceptance Criteria

1. WHEN a player disconnects during a game THEN the system SHALL detect the disconnection within a reasonable timeframe
2. WHEN a player is disconnected THEN the system SHALL replace them with an AI player automatically
3. WHEN an AI replaces a disconnected player THEN the system SHALL transfer the exact hand and game context to the AI
4. WHEN a disconnected player reconnects THEN the system SHALL allow them to reclaim their position from the AI
5. WHEN an AI replacement occurs THEN the system SHALL notify all remaining players
6. WHEN a game ends with AI replacement THEN the system SHALL save statistics only for human players who remained until the end
7. WHEN tracking AI-assisted games THEN the system SHALL categorize them separately in player statistics

#### Requirement 3.3: AI Communication and Behavior

**User Story:** As a player, I want AI players to communicate naturally within the rules so that they feel like cooperative team members.

##### Acceptance Criteria

1. WHEN an AI player wants to communicate THEN the system SHALL generate contextually appropriate messages
2. WHEN an AI generates messages THEN the system SHALL validate them through the same rule enforcement as human messages
3. WHEN an AI player makes strategic decisions THEN the system SHALL consider the cooperative nature of the game
4. WHEN an AI player cannot make valid moves THEN the system SHALL handle the situation appropriately
5. WHEN multiple AI players are present THEN the system SHALL ensure they don't communicate in ways that would be impossible for humans

### Phase 4: Expert Rules and Rule Modifications

#### Requirement 4.1: Expert Variant Implementation

**User Story:** As an experienced player, I want to enable expert rules so that I can play a more challenging version of the game.

##### Acceptance Criteria

1. WHEN creating a game THEN the system SHALL offer an option to enable expert rules
2. WHEN expert rules are enabled THEN the system SHALL require playing at least 3 cards per turn (instead of 2)
3. WHEN expert rules are enabled THEN the system SHALL deal fewer cards initially (1 player: 7 cards, 2 players: 6 cards each, 3-5 players: 5 cards each)
4. WHEN expert rules are active THEN the system SHALL maintain all other game mechanics unchanged
5. WHEN expert rules are enabled THEN the system SHALL clearly indicate this in the game UI

#### Requirement 4.2: Rule Customization Framework

**User Story:** As a player, I want to customize game rules so that I can create variations and experiment with different difficulty levels.

##### Acceptance Criteria

1. WHEN creating a game THEN the system SHALL provide options for customizing basic rule parameters
2. WHEN rule modifications are applied THEN the system SHALL validate that the modifications create a playable game
3. WHEN custom rules are active THEN the system SHALL display the active rule set to all players
4. WHEN custom rules affect AI behavior THEN the system SHALL adapt AI decision-making accordingly
5. WHEN custom rules are saved THEN the system SHALL allow reusing rule configurations in future games

*Note: Detailed rule modification options will be discussed during Phase 4 implementation planning.*

#### Requirement 4.3: Spectator System

**User Story:** As a user, I want to watch ongoing games as a spectator so that I can learn from other players and observe gameplay.

##### Acceptance Criteria

1. WHEN a player wants to spectate THEN the system SHALL allow joining active games as a spectator mid-game
2. WHEN a spectator joins THEN the system SHALL show them the current state of the four piles (top cards only)
3. WHEN spectators view the game THEN the system SHALL display how many cards have been played total
4. WHEN spectators are present THEN the system SHALL show active players a list of non-admin spectators
5. WHEN spectators watch THEN the system SHALL prevent them from communicating with players
6. WHEN spectators join THEN the system SHALL not show them previously played cards or game history
7. WHEN spectators view the game THEN the system SHALL show them only the current player's hand

#### Requirement 4.4: Admin Spectator Privileges

**User Story:** As an admin, I want enhanced spectator capabilities so that I can monitor games for moderation and system oversight.

##### Acceptance Criteria

1. WHEN an admin joins as a spectator THEN the system SHALL allow viewing any player's hand
2. WHEN an admin spectates THEN the system SHALL hide their presence from regular players
3. WHEN an admin watches games THEN the system SHALL prevent them from communicating with players
4. WHEN an admin spectates THEN the system SHALL provide the same game state information as regular spectators
5. WHEN an admin joins as a spectator THEN the system SHALL not count them in the spectator list visible to players

### Cross-Phase Technical Requirements

#### Requirement 5.1: Client-Server Architecture

**User Story:** As a system, I need a robust client-server architecture so that the game can scale from single-player to multiplayer with AI integration.

##### Acceptance Criteria

1. WHEN the client starts THEN the system SHALL establish connection with the .NET server
2. WHEN game actions occur THEN the system SHALL validate moves on both client and server sides
3. WHEN the server processes requests THEN the system SHALL maintain authoritative game state
4. WHEN the client receives updates THEN the system SHALL update the Redux store and re-render the UI
5. WHEN errors occur THEN the system SHALL handle them gracefully with appropriate user feedback

#### Requirement 5.2: AI Microservice Integration

**User Story:** As a system, I need seamless AI microservice integration so that AI functionality works consistently across all phases.

##### Acceptance Criteria

1. WHEN the server needs AI decisions THEN the system SHALL communicate with the Python AI microservice
2. WHEN the AI microservice processes requests THEN the system SHALL return decisions in the expected format
3. WHEN the AI microservice validates messages THEN the system SHALL provide sufficient game context
4. WHEN the AI microservice is unavailable THEN the system SHALL handle the failure gracefully
5. WHEN AI responses are received THEN the system SHALL validate them before applying to the game state

#### Requirement 5.3: Data Persistence and State Management

**User Story:** As a system, I need reliable state management so that game sessions persist and can be resumed appropriately.

##### Acceptance Criteria

1. WHEN games are in progress THEN the system SHALL persist game state to handle server restarts
2. WHEN players reconnect THEN the system SHALL restore their complete game context
3. WHEN games complete THEN the system SHALL store game results and statistics
4. WHEN the client manages state THEN the system SHALL use Redux for predictable state updates
5. WHEN state synchronization is needed THEN the system SHALL ensure consistency between client and server