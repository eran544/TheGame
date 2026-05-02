import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import useAppDispatch from '../hooks/useAppDispatch';
import useAppSelector from '../hooks/useAppSelector';
import { useLobbyHub } from '../hooks/useLobbyHub';
import {
  fetchLobbyStateAsync,
  leaveGameAsync,
  clearLobby,
} from '../store/slices/lobbySlice';
import * as gameApi from '../api/gameApi';
import styles from './LobbyPage.module.css';

const LobbyPage: React.FC = () => {
  const { sessionId } = useParams<{ sessionId: string }>();
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { token, user } = useAppSelector((s) => s.auth);
  const { players, maxPlayers, isExpertMode, canStart, createdBy, status, error } =
    useAppSelector((s) => s.lobby);
  const [copied, setCopied] = useState(false);

  const handleCopyId = () => {
    if (!sessionId) return;
    navigator.clipboard.writeText(sessionId).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  useLobbyHub(sessionId ?? null, token, () => {
    // When GameStarted fires, navigate to the multiplayer game
    if (sessionId) navigate(`/game/${sessionId}`);
  });

  useEffect(() => {
    if (token && sessionId) {
      dispatch(fetchLobbyStateAsync({ sessionId, token }));
    }
    return () => { dispatch(clearLobby()); };
  }, []);

  const handleStart = async () => {
    if (!token || !sessionId) return;
    await gameApi.startMultiplayerGame(sessionId, token);
    navigate(`/game/${sessionId}`);
  };

  const handleLeave = async () => {
    if (!token || !sessionId) return;
    await dispatch(leaveGameAsync({ sessionId, token }));
    navigate('/');
  };

  const isCreator = user?.id === createdBy;

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.header}>
          <h2 className={styles.title}>Waiting Room</h2>
          <span className={styles.badge}>{isExpertMode ? 'Expert' : 'Standard'}</span>
        </div>

        {sessionId && (
          <div className={styles.idBox}>
            <div className={styles.idText}>
              <span className={styles.idLabel}>Share this ID</span>
              <span className={styles.idValue}>{sessionId}</span>
            </div>
            <button className={styles.copyBtn} onClick={handleCopyId}>
              {copied ? 'Copied!' : 'Copy'}
            </button>
          </div>
        )}

        {error && <p className={styles.error}>{error}</p>}

        <div className={styles.playerList}>
          <div className={styles.playerListHeader}>
            <span>Players</span>
            <span className={styles.playerCount}>{players.length} / {maxPlayers}</span>
          </div>
          {players.map((p, i) => (
            <div key={p.userId} className={styles.playerRow}>
              <span className={styles.playerIndex}>{i + 1}</span>
              <span className={styles.playerName}>{p.username}</span>
              {p.userId === createdBy && (
                <span className={styles.hostBadge}>host</span>
              )}
            </div>
          ))}
          {Array.from({ length: maxPlayers - players.length }).map((_, i) => (
            <div key={`empty-${i}`} className={[styles.playerRow, styles.emptyRow].join(' ')}>
              <span className={styles.playerIndex}>{players.length + i + 1}</span>
              <span className={styles.emptySlot}>waiting…</span>
            </div>
          ))}
        </div>

        <div className={styles.actions}>
          {isCreator && (
            <Button
              variant="primary"
              fullWidth
              onClick={handleStart}
              disabled={!canStart || status === 'loading'}
            >
              {canStart ? 'Start Game' : `Need ${2 - players.length} more player${players.length < 1 ? 's' : ''}`}
            </Button>
          )}
          {!isCreator && (
            <p className={styles.waitingMsg}>Waiting for the host to start…</p>
          )}
          <Button variant="ghost" fullWidth onClick={handleLeave} disabled={status === 'loading'}>
            Leave
          </Button>
        </div>
      </div>
    </Layout>
  );
};

export default LobbyPage;
