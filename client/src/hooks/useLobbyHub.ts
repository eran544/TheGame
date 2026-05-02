import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import useAppDispatch from './useAppDispatch';
import { applyLobbyStateFromHub } from '../store/slices/lobbySlice';
import { applyGameStateFromHub } from '../store/slices/gameSlice';
import type { LobbyStateDto } from '../types/game';
import type { GameStateDto } from '../store/slices/gameSlice';

const BASE_URL = import.meta.env.REACT_APP_API_BASE_URL ?? 'http://localhost:5001';

export function useLobbyHub(
  sessionId: string | null,
  token: string | null,
  onGameStarted?: (dto: GameStateDto) => void
): void {
  const dispatch = useAppDispatch();
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!sessionId || !token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${BASE_URL}/gamehub`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('LobbyUpdated', (dto: LobbyStateDto) => {
      dispatch(applyLobbyStateFromHub(dto));
    });

    connection.on('GameStarted', (dto: GameStateDto) => {
      dispatch(applyGameStateFromHub(dto));
      onGameStarted?.(dto);
    });

    connection.on('PlayerLeft', () => {
      // Re-fetch lobby state is handled by the LobbyUpdated event from the joiner's side;
      // the leaver's client navigates away immediately.
    });

    connection
      .start()
      .then(() => connection.invoke('JoinGame', sessionId))
      .catch(() => {});

    connectionRef.current = connection;

    return () => {
      const conn = connectionRef.current;
      if (conn) {
        conn.invoke('LeaveGame', sessionId).then(() => conn.stop()).catch(() => conn.stop());
        connectionRef.current = null;
      }
    };
  }, [sessionId, token]);
}
