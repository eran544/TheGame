import { flip7Client } from './apiClient';
import type { Flip7AiSpec, Flip7GameState } from '../types/flip7';

/**
 * Flip 7 service REST API. Solo games are fully request/response; vs-AI and
 * online games are created here, then played over the /flip7hub SignalR
 * connection (see useFlip7Hub).
 */
const flip7Api = {
  // ----- solo -----
  createSolo: (targetScore: number | null, token: string) =>
    flip7Client.post<Flip7GameState>('/api/flip7/solo', { targetScore }, token),

  getSolo: (gameId: string, token: string) =>
    flip7Client.get<Flip7GameState>(`/api/flip7/solo/${gameId}`, token),

  soloHit: (gameId: string, token: string) =>
    flip7Client.post<Flip7GameState>(`/api/flip7/solo/${gameId}/hit`, {}, token),

  soloStay: (gameId: string, token: string) =>
    flip7Client.post<Flip7GameState>(`/api/flip7/solo/${gameId}/stay`, {}, token),

  soloChooseTarget: (gameId: string, targetPlayerId: string, token: string) =>
    flip7Client.post<Flip7GameState>(`/api/flip7/solo/${gameId}/choose-target`, { targetPlayerId }, token),

  soloNextRound: (gameId: string, token: string) =>
    flip7Client.post<Flip7GameState>(`/api/flip7/solo/${gameId}/next-round`, {}, token),

  // ----- vs-AI / online -----
  createVsAi: (aiPlayers: Flip7AiSpec[], targetScore: number | null, token: string) =>
    flip7Client.post<Flip7GameState>('/api/flip7/games/vs-ai', { aiPlayers, targetScore }, token),

  createOnline: (aiPlayers: Flip7AiSpec[], targetScore: number | null, token: string) =>
    flip7Client.post<Flip7GameState>('/api/flip7/games/online', { aiPlayers, targetScore }, token),

  getGame: (gameId: string, token: string) =>
    flip7Client.get<Flip7GameState>(`/api/flip7/games/${gameId}`, token),
};

export default flip7Api;
