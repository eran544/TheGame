/** Mirrors Flip7Server.DTOs (ASP.NET serializes PascalCase → camelCase). */

export type Flip7Mode = 'Solo' | 'VsAi' | 'Online';
export type Flip7GameStatus = 'Lobby' | 'InProgress' | 'Completed';
export type Flip7LineStatus = 'Active' | 'Stayed' | 'Frozen' | 'Busted';
export type Flip7AiStyle = 'safe' | 'balanced' | 'risky';
export type Flip7AiDifficulty = 'easy' | 'medium' | 'hard';

/** Modifier names come from the server enum: Plus2…Plus10, Times2. */
export type Flip7Modifier = 'Plus2' | 'Plus4' | 'Plus6' | 'Plus8' | 'Plus10' | 'Times2';

export interface Flip7PlayerState {
  id: string;
  userId: string;
  username: string;
  seat: number;
  isAi: boolean;
  aiStyle?: Flip7AiStyle | null;
  aiDifficulty?: Flip7AiDifficulty | null;
  cumulativeScore: number;

  numbers: number[];
  modifiers: Flip7Modifier[];
  hasSecondChance: boolean;
  status: Flip7LineStatus;
  achievedFlip7: boolean;
  roundScore: number;
}

export interface Flip7GameState {
  id: string;
  mode: Flip7Mode;
  status: Flip7GameStatus;
  targetScore: number;
  roundNumber: number;
  dealerSeat: number;
  currentPlayerId?: string | null;
  roundEnded: boolean;
  roundEndReason: string;
  winnerId?: string | null;
  players: Flip7PlayerState[];
}

export interface Flip7AiSpec {
  username?: string;
  style: Flip7AiStyle;
  difficulty: Flip7AiDifficulty;
}

export const MODIFIER_LABELS: Record<Flip7Modifier, string> = {
  Plus2: '+2',
  Plus4: '+4',
  Plus6: '+6',
  Plus8: '+8',
  Plus10: '+10',
  Times2: 'x2',
};
