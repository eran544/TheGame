import { get, post } from './apiClient';

export interface AdminDashboard {
  totalUsers: number;
  activeGames: number;
  totalCompletedGames: number;
  totalChatViolations: number;
}

export interface AdminUser {
  id: string;
  username: string;
  isAdmin: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  totalGames: number;
  perfectGames: number;
}

export interface AdminGamePlayer {
  userId: string;
  username: string;
  isAI: boolean;
}

export interface AdminGame {
  sessionId: string;
  hostUsername: string;
  playerCount: number;
  maxPlayers: number;
  startedAt: string;
  players: AdminGamePlayer[];
}

export interface CreateUserRequest {
  username: string;
  password: string;
  isAdmin: boolean;
}

const baseUrl = import.meta.env.REACT_APP_API_BASE_URL ?? 'http://localhost:5001';

async function del<T>(path: string, token: string): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
  });
  if (!response.ok) {
    let message = `HTTP ${response.status}`;
    try {
      const body = await response.json();
      if (body?.error) message = body.error;
    } catch { /* ignore */ }
    throw new Error(message);
  }
  return response.json() as Promise<T>;
}

const adminApi = {
  getDashboard: (token: string) =>
    get<AdminDashboard>('/api/admin/dashboard', token),

  getUsers: (token: string) =>
    get<AdminUser[]>('/api/admin/users', token),

  createUser: (token: string, data: CreateUserRequest) =>
    post<{ message: string }>('/api/admin/users', data, token),

  deleteUser: (token: string, userId: string) =>
    del<{ message: string }>(`/api/admin/users/${userId}`, token),

  resetPassword: (token: string, userId: string, newPassword: string) =>
    post<{ message: string }>(`/api/admin/users/${userId}/reset-password`, { newPassword }, token),

  getActiveGames: (token: string) =>
    get<AdminGame[]>('/api/admin/games', token),

  forceEndGame: (token: string, sessionId: string) =>
    post<{ message: string }>(`/api/admin/games/${sessionId}/force-end`, {}, token),

  kickPlayer: (token: string, sessionId: string, userId: string) =>
    post<{ message: string }>(`/api/admin/games/${sessionId}/kick/${userId}`, {}, token),
};

export default adminApi;
