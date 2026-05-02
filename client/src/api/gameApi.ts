import apiClient from './apiClient';
import type { GameStateDto, LobbyStateDto, TurnOutcomeDto } from '../types/game';

// ── Single-player ──────────────────────────────────────────────────────────

export function startGame(isExpertMode: boolean, token: string) {
  return apiClient.post<GameStateDto>('/api/game/start', { isExpertMode }, token);
}

export function getGameState(sessionId: string, token: string) {
  return apiClient.get<GameStateDto>(`/api/game/${sessionId}`, token);
}

export function playTurn(
  sessionId: string,
  plays: { card: number; slot: number }[],
  token: string
) {
  return apiClient.post<TurnOutcomeDto>(`/api/game/${sessionId}/turn`, { plays }, token);
}

export function abandonGame(sessionId: string, token: string) {
  return apiClient.post<void>(`/api/game/${sessionId}/abandon`, {}, token);
}

export function undoMove(sessionId: string, token: string) {
  return apiClient.post<GameStateDto>(`/api/game/${sessionId}/undo`, {}, token);
}

// ── Multiplayer ────────────────────────────────────────────────────────────

export function createMultiplayerGame(maxPlayers: number, isExpertMode: boolean, token: string) {
  return apiClient.post<LobbyStateDto>('/api/game/multiplayer/create', { maxPlayers, isExpertMode }, token);
}

export function joinGame(sessionId: string, token: string) {
  return apiClient.post<LobbyStateDto>(`/api/game/${sessionId}/join`, {}, token);
}

export function leaveGame(sessionId: string, token: string) {
  return apiClient.post<void>(`/api/game/${sessionId}/leave`, {}, token);
}

export function getLobbyState(sessionId: string, token: string) {
  return apiClient.get<LobbyStateDto>(`/api/game/${sessionId}/lobby`, token);
}

export function startMultiplayerGame(sessionId: string, token: string) {
  return apiClient.post<GameStateDto>(`/api/game/${sessionId}/multiplayer/start`, {}, token);
}
