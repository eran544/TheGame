import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HubConnection, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { FLIP7_URL } from '../api/config';
import type { Flip7GameState } from '../types/flip7';

export interface Flip7HubApi {
  state: Flip7GameState | null;
  error: string | null;
  connected: boolean;
  clearError: () => void;
  joinLobby: () => Promise<void>;
  start: () => Promise<void>;
  hit: () => Promise<void>;
  stay: () => Promise<void>;
  nextRound: () => Promise<void>;
}

/**
 * Connects to the Flip 7 hub for one game: subscribes to the game's group
 * (receiving the authoritative Flip7GameState after every action, including
 * AI turns) and exposes the hub methods. The server broadcasts full snapshots,
 * so there is no client-side game logic.
 */
export function useFlip7Hub(gameId: string | null, token: string | null): Flip7HubApi {
  const connectionRef = useRef<HubConnection | null>(null);
  const [state, setState] = useState<Flip7GameState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    if (!gameId || !token) return;

    // React StrictMode mounts effects twice in dev; `active` keeps the
    // torn-down connection's failures from surfacing as user-facing errors.
    let active = true;

    const connection = new HubConnectionBuilder()
      .withUrl(`${FLIP7_URL}/flip7hub`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('GameState', (dto: Flip7GameState) => {
      if (active) setState(dto);
    });
    connection.on('Error', (message: string) => {
      if (active) setError(message);
    });

    connection.onreconnected(() => {
      if (!active) return;
      setConnected(true);
      connection.invoke('JoinGame', gameId).catch(() => undefined);
    });
    connection.onclose(() => {
      if (active) setConnected(false);
    });

    connection
      .start()
      .then(() => {
        if (!active) return undefined;
        setConnected(true);
        return connection.invoke('JoinGame', gameId);
      })
      .catch((err) => {
        if (active) setError((err as Error).message);
      });

    connectionRef.current = connection;
    return () => {
      active = false;
      connectionRef.current = null;
      connection.stop().catch(() => undefined);
    };
  }, [gameId, token]);

  const invoke = useCallback(
    async (method: string) => {
      const connection = connectionRef.current;
      if (!gameId || !connection || connection.state !== HubConnectionState.Connected) return;
      try {
        await connection.invoke(method, gameId);
      } catch (err) {
        setError((err as Error).message);
      }
    },
    [gameId]
  );

  return {
    state,
    error,
    connected,
    clearError: useCallback(() => setError(null), []),
    joinLobby: useCallback(() => invoke('JoinLobby'), [invoke]),
    start: useCallback(() => invoke('Start'), [invoke]),
    hit: useCallback(() => invoke('Hit'), [invoke]),
    stay: useCallback(() => invoke('Stay'), [invoke]),
    nextRound: useCallback(() => invoke('NextRound'), [invoke]),
  };
}
