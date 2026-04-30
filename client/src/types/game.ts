export type GamePhase = 'lobby' | 'playing' | 'ended';

export interface Player {
  id: string;
  username: string;
  isAI: boolean;
  handCount: number;
  isActive: boolean;
  isDisconnected: boolean;
}

export interface Spectator {
  id: string;
  username: string;
}

export interface ValidMove {
  cardValue: number;
  pileIndex: number; // 0=asc1, 1=asc2, 2=desc1, 3=desc2
  isBackwardsTrick: boolean;
}

export interface ChatMessage {
  id: string;
  userId: string;
  username: string;
  message: string;
  isValidated: boolean;
  sentAt: string;
}

export interface GameState {
  sessionId: string | null;
  gamePhase: GamePhase;
  ascendingPiles: [number, number];
  descendingPiles: [number, number];
  drawPileCount: number;
  playedCardsCount: number;
  playerHand: number[];
  currentPlayer: string;
  players: Player[];
  spectators: Spectator[];
  selectedCards: number[];
  validMoves: ValidMove[];
  gameMessages: ChatMessage[];
}

export interface GameSession {
  id: string;
  createdBy: string;
  gamePhase: GamePhase;
  maxPlayers: number;
  isExpertMode: boolean;
  createdAt: string;
  startedAt: string | null;
  endedAt: string | null;
  players: Player[];
}

export interface GameResult {
  sessionId: string;
  totalCardsRemaining: number;
  isPerfectGame: boolean;
  gameDurationMinutes: number | null;
  endReason: 'completed' | 'disconnection' | 'admin_ended';
  completedAt: string;
}

export interface CardPlay {
  cardValue: number;
  pileIndex: number;
}

export interface CreateGameRequest {
  maxPlayers: number;
  isExpertMode: boolean;
}
