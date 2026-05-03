export type GamePhase = 'lobby' | 'playing' | 'ended';

export type PileSlot = 0 | 1 | 2 | 3; // 0=Asc1, 1=Asc2, 2=Desc1, 3=Desc2

export interface PlayerInGame {
  userId: string;
  username: string;
  handCount: number;
  isAI: boolean;
  isCurrentTurn: boolean;
  isDisconnected: boolean;
}

export interface LobbyPlayer {
  userId: string;
  username: string;
  playerIndex: number;
  isAI: boolean;
}

export interface LobbyStateDto {
  sessionId: string;
  gamePhase: GamePhase;
  players: LobbyPlayer[];
  maxPlayers: number;
  isExpertMode: boolean;
  canStart: boolean;
  createdBy: string;
}

export interface Spectator {
  id: string;
  username: string;
}

export interface ChatMessage {
  id: string;
  userId: string;
  username: string;
  message: string;
  isValidated: boolean;
  sentAt: string;
}

export interface ChatSendResult {
  isBlocked: boolean;
  blockReason?: string;
  violationCount: number;
  message?: ChatMessage;
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

export interface LastMovePlay {
  card: number;
  pileSlot: number;
}

export interface LastMove {
  playerUsername: string;
  plays: LastMovePlay[];
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
  canUndo: boolean;
  currentPlayerId?: string;
  players?: PlayerInGame[];
  lastMove?: LastMove;
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
