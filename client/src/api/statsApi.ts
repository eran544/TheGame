import apiClient from './apiClient';
import { PlayerStatistics } from '../types/user';
import { GameHistoryItem } from '../types/game';

export function getMyStatistics(token: string): Promise<PlayerStatistics | null> {
  return apiClient.get<PlayerStatistics | null>('/api/statistics/me', token);
}

export function getGameHistory(token: string): Promise<GameHistoryItem[]> {
  return apiClient.get<GameHistoryItem[]>('/api/statistics/history', token);
}
