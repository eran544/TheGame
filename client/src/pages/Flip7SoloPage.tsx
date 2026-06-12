import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Flip7Board from '../components/flip7/Flip7Board';
import useAppSelector from '../hooks/useAppSelector';
import flip7Api from '../api/flip7Api';
import type { Flip7GameState } from '../types/flip7';
import { deriveFlip7Events } from '../utils/flip7Events';
import styles from './Flip7GamePage.module.css';

/**
 * Single-player Flip 7 — a personal score-chase to the target. Plain
 * request/response against the solo REST endpoints; every response is the
 * authoritative new state.
 */
const Flip7SoloPage: React.FC = () => {
  const { gameId } = useParams<{ gameId: string }>();
  const navigate = useNavigate();
  const { user, token } = useAppSelector((state) => state.auth);

  const [state, setState] = useState<Flip7GameState | null>(null);
  const [feed, setFeed] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [effect, setEffect] = useState<'none' | 'bust' | 'win'>('none');
  const prevState = useRef<Flip7GameState | null>(null);

  const applyState = useCallback((next: Flip7GameState) => {
    const events = deriveFlip7Events(prevState.current, next);
    setFeed((old) => [...old, ...events]);

    const me = next.players[0];
    const wasMe = prevState.current?.players[0];
    if (me && wasMe && me.status === 'Busted' && wasMe.status !== 'Busted') {
      setEffect('bust');
      setTimeout(() => setEffect('none'), 900);
    } else if (me && me.achievedFlip7 && !wasMe?.achievedFlip7) {
      setEffect('win');
      setTimeout(() => setEffect('none'), 1200);
    }

    prevState.current = next;
    setState(next);
  }, []);

  useEffect(() => {
    if (!gameId || !token) return;
    flip7Api
      .getSolo(gameId, token)
      .then(applyState)
      .catch((err) => setError((err as Error).message));
  }, [gameId, token, applyState]);

  const act = async (action: 'hit' | 'stay' | 'next-round') => {
    if (!gameId || !token || busy) return;
    setBusy(true);
    setError(null);
    try {
      const next =
        action === 'hit'
          ? await flip7Api.soloHit(gameId, token)
          : action === 'stay'
            ? await flip7Api.soloStay(gameId, token)
            : await flip7Api.soloNextRound(gameId, token);
      applyState(next);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

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
        <div className={styles.orbCyan} />
        <div className={styles.orbPink} />
        <div className={styles.headerArea}>
          <div className={styles.titleBlock}>
            <h1 className={styles.title}>
              Flip <span>7</span>
            </h1>
            <p className={styles.tagline}>Solo — chase {state?.targetScore ?? 200} points</p>
          </div>
          {state && <div className={styles.roundTracker}>Round {state.roundNumber}</div>}
        </div>

        {error && <div className={styles.errorBanner}>{error}</div>}

        {!state && !error && <p className={styles.loadingText}>Dealing…</p>}

        {state && user && (
          <Flip7Board
            state={state}
            myUserId={user.id}
            feed={feed}
            busy={busy}
            onHit={() => act('hit')}
            onStay={() => act('stay')}
            onNextRound={() => act('next-round')}
            onExit={() => navigate('/flip7')}
          />
        )}
      </div>
    </Layout>
  );
};

export default Flip7SoloPage;
