import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import useAppDispatch from './useAppDispatch';
import { applyGameStateFromHub } from '../store/slices/gameSlice';
import type { GameStateDto } from '../store/slices/gameSlice';

const BASE_URL = import.meta.env.REACT_APP_API_BASE_URL ?? 'http://localhost:5001';

export function useGameHub(sessionId: string | null, token: string | null): void {
  const dispatch = useAppDispatch();
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!sessionId || !token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${BASE_URL}/gamehub`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('GameStateUpdated', (dto: GameStateDto) => {
      dispatch(applyGameStateFromHub(dto));
    });

    connection
      .start()
      .then(() => connection.invoke('JoinGame', sessionId))
      .catch(() => {
        // Hub connection failure is non-fatal — the HTTP response is the
        // primary source of truth; SignalR provides supplementary real-time
        // updates for Phase 2 multiplayer scenarios.
      });

    connectionRef.current = connection;

    return () => {
      const conn = connectionRef.current;
      if (conn) {
        conn
          .invoke('LeaveGame', sessionId)
          .catch(() => {})
          .finally(() => conn.stop());
        connectionRef.current = null;
      }
    };
  }, [sessionId, token, dispatch]);
}
