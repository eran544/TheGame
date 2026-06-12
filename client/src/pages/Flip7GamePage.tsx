import React, { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import Flip7Board from '../components/flip7/Flip7Board';
import useAppSelector from '../hooks/useAppSelector';
import { useFlip7Hub } from '../hooks/useFlip7Hub';
import type { Flip7GameState } from '../types/flip7';
import { deriveFlip7Events } from '../utils/flip7Events';
import styles from './Flip7GamePage.module.css';

/**
 * Vs-AI and online Flip 7, driven entirely by the /flip7hub SignalR
 * connection: the server broadcasts the authoritative state after every
 * action (including AI turns). While an online game is in its lobby this
 * page shows the joined players and lets the creator start the game.
 */
const Flip7GamePage: React.FC = () => {
  const { gameId } = useParams<{ gameId: string }>();
  const navigate = useNavigate();
  const { user, token } = useAppSelector((state) => state.auth);

  const hub = useFlip7Hub(gameId ?? null, token);
  const { state } = hub;

  const [feed, setFeed] = useState<string[]>([]);
  const [effect, setEffect] = useState<'none' | 'bust' | 'win'>('none');
  const [copied, setCopied] = useState(false);
  const prevState = useRef<Flip7GameState | null>(null);

  // Derive the feed + screen effects from each authoritative snapshot.
  useEffect(() => {
    if (!state) return;
    const events = deriveFlip7Events(prevState.current, state);
    if (events.length > 0) setFeed((old) => [...old, ...events]);

    if (user) {
      const me = state.players.find((p) => p.userId === user.id && !p.isAi);
      const wasMe = prevState.current?.players.find((p) => p.userId === user.id && !p.isAi);
      if (me && wasMe && me.status === 'Busted' && wasMe.status !== 'Busted') {
        setEffect('bust');
        setTimeout(() => setEffect('none'), 900);
      } else if (me && me.achievedFlip7 && !wasMe?.achievedFlip7) {
        setEffect('win');
        setTimeout(() => setEffect('none'), 1200);
      }
    }

    prevState.current = state;
  }, [state, user]);

  // If I'm not seated yet in an online lobby, join it.
  useEffect(() => {
    if (!state || !user || state.status !== 'Lobby') return;
    const seated = state.players.some((p) => p.userId === user.id && !p.isAi);
    if (!seated) {
      hub.joinLobby();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state?.id, state?.status, state?.players.length, user?.id]);

  const copyGameId = async () => {
    if (!gameId) return;
    try {
      await navigator.clipboard.writeText(gameId);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // Clipboard unavailable — the id is visible to copy manually.
    }
  };

  const inLobby = state?.status === 'Lobby';
  // The creator is the first human seat (seat 0 is always the creator).
  const isCreator =
    !!state && !!user && state.players.some((p) => p.seat === 0 && p.userId === user.id && !p.isAi);

  const pageClass = [
    styles.page,
    effect === 'bust' ? styles.effectBust : '',
    effect === 'win' ? styles.effectWin : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <Layout showHeader>
      <div className={pageClass}>
        <div className={styles.headerArea}>
          <div className={styles.titleBlock}>
            <h1 className={styles.title}>
              Flip <span>7</span>
            </h1>
            <p className={styles.tagline}>
              {state?.mode === 'Online' ? 'Online match' : 'Vs AI'} — first to{' '}
              {state?.targetScore ?? 200}
            </p>
          </div>
          {state && !inLobby && <div className={styles.roundTracker}>Round {state.roundNumber}</div>}
        </div>

        {hub.error && (
          <div className={styles.errorBanner} onClick={hub.clearError}>
            {hub.error}
          </div>
        )}

        {!state && !hub.error && (
          <p className={styles.loadingText}>{hub.connected ? 'Joining game…' : 'Connecting…'}</p>
        )}

        {state && inLobby && (
          <div className={styles.setupCard}>
            <h2>Lobby</h2>
            <p>Share this game ID so friends can join:</p>
            <div className={styles.lobbyIdRow}>
              <code className={styles.lobbyId}>{state.id}</code>
              <Button variant="ghost" size="sm" onClick={copyGameId}>
                {copied ? 'Copied!' : 'Copy'}
              </Button>
            </div>

            <div className={styles.lobbyPlayers}>
              {state.players.map((p) => (
                <div key={p.id} className={styles.lobbyPlayerRow}>
                  <span>
                    {p.isAi ? '🤖' : '🎮'} {p.username}
                    {user && p.userId === user.id && !p.isAi ? ' (you)' : ''}
                  </span>
                  {p.isAi && (
                    <span className={styles.lobbyAiMeta}>
                      {p.aiStyle} · {p.aiDifficulty}
                    </span>
                  )}
                </div>
              ))}
            </div>

            {isCreator ? (
              <Button variant="primary" size="lg" onClick={hub.start}>
                Start Game
              </Button>
            ) : (
              <p className={styles.lobbyHint}>Waiting for the host to start…</p>
            )}
          </div>
        )}

        {state && !inLobby && user && (
          <Flip7Board
            state={state}
            myUserId={user.id}
            feed={feed}
            onHit={hub.hit}
            onStay={hub.stay}
            onNextRound={hub.nextRound}
            onExit={() => navigate('/flip7')}
          />
        )}
      </div>
    </Layout>
  );
};

export default Flip7GamePage;
