export type GamePhase = 'lobby' | 'playing' | 'ended';

export type PileSlot = 0 | 1 | 2 | 3; // 0=Asc1, 1=Asc2, 2=Desc1, 3=Desc2

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
  pileIndex: number;
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

export interface CardPlay {
  cardValue: number;
  pileIndex: number;
}

export interface CreateGameRequest {
  maxPlayers: number;
  isExpertMode: boolean;
}

export interface StagedPlay {
  card: number;
  pileSlot: PileSlot;
}

export interface FinalScore {
  cardsRemaining: number;
  isPerfectGame: boolean;
  rating: 'Perfect' | 'Excellent' | 'TryAgain';
}

export interface PileTopsDto {
  ascending1: number;
  ascending2: number;
  descending1: number;
  descending2: number;
}

export interface GameStateDto {
  sessionId: string;
  gamePhase: GamePhase;
  isExpertMode: boolean;
  piles: PileTopsDto;
  drawPileCount: number;
  playedCardsCount: number;
  hand: number[];
  minCardsThisTurn: number;
  finalScore: FinalScore | null;
}

export interface TurnOutcomeDto {
  state: GameStateDto;
  gameEnded: boolean;
  endReason: string | null;
}

export interface GameHistoryItem {
  sessionId: string;
  playedAt: string;
  cardsRemaining: number;
  isPerfectGame: boolean;
  rating: 'Perfect' | 'Excellent' | 'TryAgain';
  durationMinutes: number | null;
  endReason: string;
  isExpertMode: boolean;
}
