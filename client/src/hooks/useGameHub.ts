import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import useAppDispatch from './useAppDispatch';
import { applyGameStateFromHub, gameEndedFromHub, loadGameAsync } from '../store/slices/gameSlice';
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

    connection.on('GameEnded', (payload: GameStateDto | { reason: string }) => {
      if ('gamePhase' in payload) {
        // Full state DTO — apply it (game ended naturally via last turn)
        dispatch(applyGameStateFromHub(payload));
      } else {
        // Simple reason object — game ended due to disconnection or leave
        dispatch(gameEndedFromHub({ reason: payload.reason }));
      }
    });

    // PlayerLeft during an active game is handled by GameEnded; during lobby
    // it is handled by LobbyUpdated. No additional action needed here.
    connection.on('PlayerLeft', () => {});

    connection.onreconnected(() => {
      connection.invoke('JoinGame', sessionId).catch(() => {});
      // Reload the latest server state in case we missed updates while disconnected.
      dispatch(loadGameAsync({ sessionId, token }));
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
        conn.invoke('LeaveGame', sessionId).then(() => conn.stop(), () => conn.stop());
        connectionRef.current = null;
      }
    };
  }, [sessionId, token, dispatch]);
}
