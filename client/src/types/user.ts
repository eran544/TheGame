export interface User {
  id: string;
  username: string;
  isAdmin: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AuthResult {
  token: string;
  user: User;
}

export interface RegisterRequest {
  username: string;
  password: string;
  passwordConfirmation: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface PlayerStatistics {
  userId: string;
  totalGames: number;
  perfectGames: number;
  bestScore: number | null;
  averageRemainingCards: number;
  totalPlayTimeMinutes: number;
  aiAssistedGames: number;
  lastUpdated: string;
}
