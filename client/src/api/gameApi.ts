import apiClient from './apiClient';
import type { GameStateDto, TurnOutcomeDto } from '../types/game';

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
